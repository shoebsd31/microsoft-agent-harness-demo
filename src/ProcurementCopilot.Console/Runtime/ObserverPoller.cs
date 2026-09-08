using Microsoft.Agents.AI;
using ProcurementCopilot.Agent.Sessions;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>Change detected by the <see cref="ObserverPoller"/>.</summary>
/// <param name="Status">The latest status file.</param>
/// <param name="NewTools">Tool events not seen before.</param>
/// <param name="ActivityChanged">True when the driver's activity changed since the last poll.</param>
/// <param name="SessionReloaded">True when the session file changed and was re-read.</param>
public sealed record ObserverUpdate(SessionStatus Status, IReadOnlyList<ToolEvent> NewTools, bool ActivityChanged, bool SessionReloaded);

/// <summary>
/// Polls the driver's <c>{id}.status.json</c> and the session file while this instance observes a session,
/// and raises <see cref="Changed"/> with what is new. Works for both the classic and the TUI front-ends.
/// </summary>
public sealed class ObserverPoller : IDisposable
{
    private readonly StatusPublisher _status;
    private readonly AgentBootstrapper _bootstrapper;
    private readonly string _sessionsDirectory;
    private Timer? _timer;
    private DateTimeOffset _lastStatusAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastToolAt = DateTimeOffset.MinValue;
    private DateTime _lastSessionWrite = DateTime.MinValue;
    private string _lastActivity = string.Empty;
    private int _polling;

    /// <summary>Initializes the poller.</summary>
    public ObserverPoller(StatusPublisher status, AgentBootstrapper bootstrapper, string sessionsDirectory)
    {
        _status = status;
        _bootstrapper = bootstrapper;
        _sessionsDirectory = sessionsDirectory;
    }

    /// <summary>Raised on a thread-pool thread whenever something changed.</summary>
    public event Action<ObserverUpdate>? Changed;

    /// <summary>Starts polling the state's session.</summary>
    public void Start(AppState state, TimeSpan interval)
    {
        Stop();
        _lastToolAt = state.ObservedStatus?.RecentTools.LastOrDefault()?.At ?? DateTimeOffset.MinValue;
        _lastStatusAt = state.ObservedStatus?.UpdatedAt ?? DateTimeOffset.MinValue;
        _lastActivity = state.ObservedStatus?.Activity ?? string.Empty;
        _lastSessionWrite = SessionWriteTime(state);
        _timer = new Timer(_ => Poll(state), null, interval, interval);
    }

    /// <summary>Stops polling.</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>Runs one poll immediately (used by tests and by <c>/refresh</c>-style commands).</summary>
    public void Poll(AppState state)
    {
        if (state.Role != SessionRole.Observer || Interlocked.Exchange(ref _polling, 1) == 1)
        {
            return;
        }

        try
        {
            PollCore(state);
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    private void PollCore(AppState state)
    {
        Guid id = SessionIdentity.GetOrCreate(state.Session);
        SessionStatus? status = _status.Read(id);
        bool reloaded = ReloadSessionIfChanged(state);
        if (status is null)
        {
            if (reloaded)
            {
                Changed?.Invoke(new ObserverUpdate(state.ObservedStatus ?? new SessionStatus { SessionId = id }, [], false, true));
            }

            return;
        }

        if (status.UpdatedAt == _lastStatusAt && !reloaded)
        {
            return;
        }

        List<ToolEvent> fresh = status.RecentTools.Where(t => t.At > _lastToolAt).ToList();
        bool activityChanged = !string.Equals(status.Activity, _lastActivity, StringComparison.Ordinal);
        _lastStatusAt = status.UpdatedAt;
        _lastActivity = status.Activity;
        if (fresh.Count > 0)
        {
            _lastToolAt = fresh[^1].At;
        }

        state.ObservedStatus = status;
        Changed?.Invoke(new ObserverUpdate(status, fresh, activityChanged, reloaded));
    }

    private bool ReloadSessionIfChanged(AppState state)
    {
        DateTime written = SessionWriteTime(state);
        if (written == _lastSessionWrite)
        {
            return false;
        }

        _lastSessionWrite = written;
        try
        {
            AgentSession? session = _bootstrapper.TryResumeAsync(state.Agent, SessionIdentity.GetOrCreate(state.Session)).GetAwaiter().GetResult();
            if (session is not null)
            {
                state.Session = session;
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            // The driver is mid-write; the next poll retries.
        }

        return false;
    }

    private DateTime SessionWriteTime(AppState state)
    {
        string path = Path.Combine(_sessionsDirectory, SessionIdentity.GetOrCreate(state.Session).ToString("N") + ".json");
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
    }

    /// <inheritdoc />
    public void Dispose() => Stop();
}
