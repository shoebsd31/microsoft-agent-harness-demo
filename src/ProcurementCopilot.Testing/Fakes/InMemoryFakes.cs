using System.Text.Json;
using ProcurementCopilot.Application.Abstractions;

namespace ProcurementCopilot.Testing.Fakes;

/// <summary>Session store that keeps everything in memory and counts writes.</summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly Dictionary<Guid, (JsonElement Json, SessionSummary Meta)> _sessions = [];

    /// <summary>Total number of <see cref="SaveAsync"/> calls.</summary>
    public int WriteCount { get; private set; }

    /// <summary>Ids saved so far.</summary>
    public IReadOnlyCollection<Guid> Ids => _sessions.Keys;

    /// <inheritdoc />
    public Task SaveAsync(Guid sessionId, JsonElement serializedSession, string? title, CancellationToken cancellationToken = default)
    {
        WriteCount++;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SessionSummary meta = _sessions.TryGetValue(sessionId, out var existing) ? existing.Meta with { UpdatedAt = now } : new SessionSummary(sessionId, now, now, title ?? "(untitled)");
        _sessions[sessionId] = (serializedSession.Clone(), meta);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<JsonElement?> LoadAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sessions.TryGetValue(sessionId, out var entry) ? entry.Json : (JsonElement?)null);

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SessionSummary>>(_sessions.Values.Select(v => v.Meta).OrderByDescending(m => m.UpdatedAt).ToList());
}

/// <summary>Outbox that records drafts and documents in memory.</summary>
public sealed class InMemoryOutbox : IOutbox
{
    /// <summary>Drafts saved.</summary>
    public List<EmailDraft> Drafts { get; } = [];

    /// <summary>Documents saved, keyed by relative path.</summary>
    public Dictionary<string, string> Documents { get; } = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<string> SaveDraftAsync(EmailDraft draft, CancellationToken cancellationToken = default)
    {
        Drafts.Add(draft);
        return Task.FromResult($"output/outbox/{draft.Id:N}.md");
    }

    /// <inheritdoc />
    public Task<string> SaveDocumentAsync(string relativePath, string content, CancellationToken cancellationToken = default)
    {
        Documents["output/" + relativePath] = content;
        return Task.FromResult("output/" + relativePath);
    }
}

/// <summary>Audit log that records entries in memory.</summary>
public sealed class InMemoryAuditLog : IAuditLog
{
    /// <summary>Approval records.</summary>
    public List<ApprovalRecord> Approvals { get; } = [];

    /// <summary>Action records.</summary>
    public List<ActionRecord> Actions { get; } = [];

    /// <inheritdoc />
    public Task RecordApprovalAsync(ApprovalRecord record, CancellationToken cancellationToken = default)
    {
        Approvals.Add(record);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordActionAsync(ActionRecord record, CancellationToken cancellationToken = default)
    {
        Actions.Add(record);
        return Task.CompletedTask;
    }
}

/// <summary>A clock frozen at a fixed instant.</summary>
public sealed class FixedClock(DateTimeOffset now) : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = now;
}

/// <summary>Evaluation state held in a plain field.</summary>
public sealed class InMemoryEvaluationStateStore : IEvaluationStateStore
{
    /// <summary>The current state.</summary>
    public EvaluationState State { get; set; } = new();

    /// <inheritdoc />
    public EvaluationState Get() => State;

    /// <inheritdoc />
    public void Save(EvaluationState state) => State = state;
}

/// <summary>Mode accessor with a settable mode.</summary>
public sealed class StaticModeAccessor(string mode) : IAgentModeAccessor
{
    /// <summary>The mode to report.</summary>
    public string Mode { get; set; } = mode;

    /// <inheritdoc />
    public ValueTask<string> GetCurrentModeAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Mode);
}

/// <summary>Shell executor that returns canned output, optionally after a delay (to test timeouts).</summary>
public sealed class FakeShellExecutor : IShellExecutor
{
    /// <summary>Output returned for every command.</summary>
    public string Stdout { get; set; } = "ok";

    /// <summary>Delay before returning; when longer than the tool timeout the call reports a timeout.</summary>
    public TimeSpan Delay { get; set; }

    /// <summary>Commands received.</summary>
    public List<string> Commands { get; } = [];

    /// <inheritdoc />
    public string ShellFamily => "fake";

    /// <inheritdoc />
    public async Task<ShellExecution> RunAsync(string command, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        Commands.Add(command);
        if (Delay > timeout)
        {
            await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);
            return new ShellExecution(string.Empty, "timed out", 124, true, false);
        }

        return new ShellExecution(Stdout, string.Empty, 0, false, false);
    }
}
