using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProcurementCopilot.Application.Abstractions;

namespace ProcurementCopilot.Agent.Sessions;

/// <summary>
/// Decorates the harness <see cref="InMemoryChatHistoryProvider"/> and writes the whole serialized session to the
/// <see cref="ISessionStore"/> after <b>every</b> model call. The harness invokes this provider once per service call
/// inside the tool-calling loop, so a crash mid-run loses at most the call in flight.
/// </summary>
public sealed class CheckpointingChatHistoryProvider : ChatHistoryProvider
{
    private readonly InMemoryChatHistoryProvider _inner;
    private readonly ISessionStore _store;
    private readonly ILogger _logger;

    /// <summary>Initializes the provider.</summary>
    public CheckpointingChatHistoryProvider(InMemoryChatHistoryProvider inner, ISessionStore store, ILogger<CheckpointingChatHistoryProvider>? logger = null)
        : base(null, null, null)
    {
        _inner = inner;
        _store = store;
        _logger = logger ?? NullLogger<CheckpointingChatHistoryProvider>.Instance;
    }

    /// <summary>Number of checkpoints written since the provider was created.</summary>
    public int CheckpointCount { get; private set; }

    /// <inheritdoc />
    public override IReadOnlyList<string> StateKeys => _inner.StateKeys;

    /// <summary>Returns the messages stored for the session.</summary>
    public List<ChatMessage> GetMessages(AgentSession session) => _inner.GetMessages(session);

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null) =>
        base.GetService(serviceType, serviceKey) ?? _inner.GetService(serviceType, serviceKey);

    /// <inheritdoc />
    protected override ValueTask<IEnumerable<ChatMessage>> InvokingCoreAsync(InvokingContext context, CancellationToken cancellationToken = default) =>
        _inner.InvokingAsync(context, cancellationToken);

    /// <inheritdoc />
    protected override async ValueTask InvokedCoreAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        await _inner.InvokedAsync(context, cancellationToken).ConfigureAwait(false);
        if (context.Session is not null)
        {
            await CheckpointAsync(context.Agent, context.Session, context.RequestMessages, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CheckpointAsync(AIAgent agent, AgentSession session, IEnumerable<ChatMessage> requestMessages, CancellationToken cancellationToken)
    {
        try
        {
            Guid id = SessionIdentity.GetOrCreate(session);
            JsonElement serialized = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken).ConfigureAwait(false);
            string? title = requestMessages.FirstOrDefault(m => m.Role == ChatRole.User && !string.IsNullOrWhiteSpace(m.Text))?.Text;
            await _store.SaveAsync(id, serialized, Truncate(title), cancellationToken).ConfigureAwait(false);
            CheckpointCount++;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Session checkpoint failed; continuing without persistence for this call.");
        }
    }

    private static string? Truncate(string? text) => text is null ? null : text.Length <= 80 ? text : text[..80] + "…";
}
