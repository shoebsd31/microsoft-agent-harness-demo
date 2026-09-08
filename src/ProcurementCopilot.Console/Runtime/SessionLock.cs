using System.Diagnostics;
using System.Text.Json;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>
/// Exclusive "driver" lock for a session: <c>{sessionId}.lock</c> in the sessions directory holds the owning process and a
/// heartbeat. A lock is stale when the owning process is gone or the heartbeat is older than <see cref="StaleAfter"/>.
/// Observers never take the lock; a driver must hold it before sending prompts to the session.
/// </summary>
public sealed class SessionLock : IDisposable
{
    /// <summary>Heartbeat interval.</summary>
    public static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(10);

    /// <summary>Age after which a lock without a live process is considered abandoned.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(45);

    private readonly string _directory;
    private Timer? _timer;

    /// <summary>Initializes the lock helper for a sessions directory.</summary>
    public SessionLock(string directory) => _directory = directory;

    /// <summary>Gets the session currently held by this process, if any.</summary>
    public Guid? Held { get; private set; }

    /// <summary>Name of this instance as written into lock and status files.</summary>
    public static string InstanceName => $"{Environment.MachineName}:{Environment.ProcessId}";

    /// <summary>Reads the lock for a session, or <see langword="null"/>.</summary>
    public SessionLockInfo? Read(Guid sessionId)
    {
        string path = PathFor(sessionId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), StatusJsonContext.Default.SessionLockInfo);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>Returns the live owner of a session, or <see langword="null"/> when nobody (or a dead process) holds it.</summary>
    public SessionLockInfo? LiveOwner(Guid sessionId)
    {
        SessionLockInfo? info = Read(sessionId);
        return info is not null && IsAlive(info) ? info : null;
    }

    /// <summary>Tries to become the driver. Fails when another live instance holds the lock.</summary>
    public bool TryAcquire(Guid sessionId, out SessionLockInfo? owner)
    {
        owner = LiveOwner(sessionId);
        if (owner is not null && owner.Pid != Environment.ProcessId)
        {
            return false;
        }

        Release();
        Write(sessionId);
        Held = sessionId;
        _timer = new Timer(_ => Write(sessionId), null, Heartbeat, Heartbeat);
        owner = null;
        return true;
    }

    /// <summary>Releases the lock held by this process, if any.</summary>
    public void Release()
    {
        _timer?.Dispose();
        _timer = null;
        if (Held is { } held)
        {
            try
            {
                File.Delete(PathFor(held));
            }
            catch (IOException)
            {
                // Best effort: a stale file is ignored by the next reader.
            }
        }

        Held = null;
    }

    /// <inheritdoc />
    public void Dispose() => Release();

    private static bool IsAlive(SessionLockInfo info)
    {
        if (DateTimeOffset.UtcNow - info.HeartbeatAt > StaleAfter)
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById(info.Pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false; // no such process
        }
        catch (InvalidOperationException)
        {
            return false; // exited between the lookup and the check
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return true; // exists but cannot be inspected (another user / elevated); trust the fresh heartbeat
        }
    }

    private void Write(Guid sessionId)
    {
        Directory.CreateDirectory(_directory);
        var info = new SessionLockInfo { SessionId = sessionId, Instance = InstanceName, Pid = Environment.ProcessId, AcquiredAt = Read(sessionId)?.AcquiredAt ?? DateTimeOffset.UtcNow, HeartbeatAt = DateTimeOffset.UtcNow };
        string temp = PathFor(sessionId) + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(info, StatusJsonContext.Default.SessionLockInfo));
        File.Move(temp, PathFor(sessionId), overwrite: true);
    }

    private string PathFor(Guid sessionId) => Path.Combine(_directory, sessionId.ToString("N") + ".lock");
}
