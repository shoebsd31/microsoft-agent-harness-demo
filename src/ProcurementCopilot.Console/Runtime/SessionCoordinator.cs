using Microsoft.Agents.AI;
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Agent.State;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Infrastructure.SqlServer;
using ProcurementCopilot.Infrastructure.Telemetry;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>
/// Owns the driver/observer relationship of this instance with its session: takes and releases the session lock,
/// publishes the live status file, and reloads sessions when observing.
/// </summary>
public sealed class SessionCoordinator : IDisposable
{
    private readonly SessionLock _lock;
    private readonly StatusPublisher _status;
    private readonly ISessionStore _store;
    private readonly AgentBootstrapper _bootstrapper;
    private readonly SpanRingBuffer _spans;
    private readonly DataProviderSelection _data;

    /// <summary>Initializes the coordinator.</summary>
    public SessionCoordinator(SessionLock sessionLock, StatusPublisher status, ISessionStore store, AgentBootstrapper bootstrapper, SpanRingBuffer spans, DataProviderSelection data)
    {
        _lock = sessionLock;
        _status = status;
        _store = store;
        _bootstrapper = bootstrapper;
        _spans = spans;
        _data = data;
    }

    /// <summary>Live status publisher of this instance.</summary>
    public StatusPublisher Status => _status;

    /// <summary>Who drives a session right now, or <see langword="null"/> when it is free.</summary>
    public SessionLockInfo? LiveOwner(Guid sessionId) => _lock.LiveOwner(sessionId);

    /// <summary>Tries to become the driver of the state's session. On failure the state becomes an observer.</summary>
    public async Task<SessionLockInfo?> TryDriveAsync(AppState state)
    {
        Guid id = SessionIdentity.GetOrCreate(state.Session);
        if (!_lock.TryAcquire(id, out SessionLockInfo? owner))
        {
            state.Role = SessionRole.Observer;
            state.ObservedStatus = _status.Read(id);
            return owner;
        }

        state.Role = SessionRole.Driver;
        state.ObservedStatus = null;
        string mode = await state.GetModeAsync().ConfigureAwait(false);
        _status.Update(s =>
        {
            s.SessionId = id;
            s.DataProvider = _data.Provider;
            s.Mode = mode;
            s.Activity = "idle";
            s.RecentTools.Clear();
            s.RecentSpans.Clear();
            s.Tasks = [];
            s.Outstanding = [];
        });
        return null;
    }

    /// <summary>Loads a stored session for observation (never takes the lock).</summary>
    public async Task<bool> AttachAsync(AppState state, Guid sessionId)
    {
        AgentSession? session = await _bootstrapper.TryResumeAsync(state.Agent, sessionId).ConfigureAwait(false);
        if (session is null)
        {
            return false;
        }

        state.Session = session;
        state.Role = SessionRole.Observer;
        state.ObservedStatus = _status.Read(sessionId);
        return true;
    }

    /// <summary>Observer → driver: reloads the latest session file and takes the lock if it is free.</summary>
    public async Task<SessionLockInfo?> TakeoverAsync(AppState state)
    {
        Guid id = SessionIdentity.GetOrCreate(state.Session);
        SessionLockInfo? owner = _lock.LiveOwner(id);
        if (owner is not null && owner.Pid != Environment.ProcessId)
        {
            return owner;
        }

        AgentSession? fresh = await _bootstrapper.TryResumeAsync(state.Agent, id).ConfigureAwait(false);
        if (fresh is not null)
        {
            state.Session = fresh;
        }

        return await TryDriveAsync(state).ConfigureAwait(false);
    }

    /// <summary>Driver → observer: saves, marks the status as released and drops the lock.</summary>
    public async Task DetachAsync(AppState state)
    {
        await SaveAsync(state, null).ConfigureAwait(false);
        _status.Update(s => s.Activity = "exited");
        _status.Flush();
        _lock.Release();
        state.Role = SessionRole.Observer;
        state.ObservedStatus = _status.Read(SessionIdentity.GetOrCreate(state.Session));
    }

    /// <summary>Releases the lock of the current session when this instance drives it (used before switching sessions).</summary>
    public async Task DetachIfDrivingAsync(AppState state)
    {
        if (state.Role == SessionRole.Driver)
        {
            await DetachAsync(state).ConfigureAwait(false);
        }
    }

    /// <summary>Publishes end-of-turn facts (mode, usage, tasks, spans, compaction, outstanding items) and saves the session.</summary>
    public async Task CompleteTurnAsync(AppState state, string? title, IReadOnlyList<string> outstanding)
    {
        string mode = await state.GetModeAsync().ConfigureAwait(false);
        _status.Update(s =>
        {
            s.Mode = mode;
            s.Usage = new UsageSnapshot(state.Usage.LastInputTokens, state.Usage.LastOutputTokens, state.Usage.TotalTokens, state.Usage.Reports);
            s.Tasks = state.Tasks.Tasks.Values.ToList();
            s.CompactionCount = state.Factory.CompactionStrategy?.CompactionCount ?? 0;
            s.RecentSpans = _spans.Recent(30).Select(x => new SpanEvent(x.Name, x.Source, x.Duration.TotalMilliseconds, x.Status)).ToList();
            s.Outstanding = outstanding.ToList();
        });
        await SaveAsync(state, title).ConfigureAwait(false);
    }

    /// <summary>Publishes a mode change made through <c>/mode</c>.</summary>
    public void PublishMode(string mode) => _status.Update(s => s.Mode = mode);

    /// <summary>Saves the session when this instance drives it.</summary>
    public async Task SaveAsync(AppState state, string? title)
    {
        if (state.Role != SessionRole.Driver)
        {
            return;
        }

        using (SessionEvaluationStateStore.UseSession(state.Session))
        {
            await SessionPersistence.SaveAsync(state.Agent, state.Session, _store, title).ConfigureAwait(false);
        }
    }

    /// <summary>Marks the status as exited and releases the lock; safe to call twice.</summary>
    public void Exit()
    {
        if (_lock.Held is not null)
        {
            _status.Update(s => s.Activity = "exited");
            _status.Flush();
        }

        _lock.Release();
    }

    /// <inheritdoc />
    public void Dispose() => Exit();
}
