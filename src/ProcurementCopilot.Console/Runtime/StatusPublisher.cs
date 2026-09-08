using System.Text.Json;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>
/// Publishes the driving instance's <see cref="SessionStatus"/> to <c>{sessionId}.status.json</c> (atomic write, throttled)
/// so observers can follow along. Also reads status files for the observer mode.
/// </summary>
public sealed class StatusPublisher
{
    private const int MaxRecentTools = 40;
    private const int MaxRecentSpans = 30;
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(200);

    private readonly string _directory;
    private readonly object _gate = new();
    private DateTimeOffset _lastWrite = DateTimeOffset.MinValue;
    private bool _dirty;
    private Timer? _flush;

    /// <summary>Initializes the publisher for a sessions directory.</summary>
    public StatusPublisher(string directory) => _directory = directory;

    /// <summary>Current status (mutate through <see cref="Update"/>).</summary>
    public SessionStatus Status { get; } = new() { Instance = SessionLock.InstanceName, Pid = Environment.ProcessId };

    /// <summary>Applies a change and schedules a write.</summary>
    public void Update(Action<SessionStatus> change)
    {
        lock (_gate)
        {
            change(Status);
            Status.UpdatedAt = DateTimeOffset.UtcNow;
            if (Status.RecentTools.Count > MaxRecentTools)
            {
                Status.RecentTools.RemoveRange(0, Status.RecentTools.Count - MaxRecentTools);
            }

            if (Status.RecentSpans.Count > MaxRecentSpans)
            {
                Status.RecentSpans.RemoveRange(MaxRecentSpans, Status.RecentSpans.Count - MaxRecentSpans);
            }

            _dirty = true;
            TimeSpan sinceLast = DateTimeOffset.UtcNow - _lastWrite;
            if (sinceLast >= MinInterval)
            {
                WriteNow();
            }
            else
            {
                _flush ??= new Timer(_ => Flush(), null, MinInterval - sinceLast, Timeout.InfiniteTimeSpan);
            }
        }
    }

    /// <summary>Writes immediately (used on exit).</summary>
    public void Flush()
    {
        lock (_gate)
        {
            _flush?.Dispose();
            _flush = null;
            if (_dirty)
            {
                WriteNow();
            }
        }
    }

    /// <summary>Reads the status of any session, or <see langword="null"/> when none has been published.</summary>
    public SessionStatus? Read(Guid sessionId)
    {
        string path = PathFor(sessionId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), StatusJsonContext.Default.SessionStatus);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    private void WriteNow()
    {
        if (Status.SessionId == Guid.Empty)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_directory);
            string temp = PathFor(Status.SessionId) + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Status, StatusJsonContext.Default.SessionStatus));
            File.Move(temp, PathFor(Status.SessionId), overwrite: true);
            _lastWrite = DateTimeOffset.UtcNow;
            _dirty = false;
        }
        catch (IOException)
        {
            // A missed status write is harmless; the next update retries.
        }
    }

    private string PathFor(Guid sessionId) => Path.Combine(_directory, sessionId.ToString("N") + ".status.json");
}
