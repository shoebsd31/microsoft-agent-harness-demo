using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ProcurementCopilot.Agent.Background;

/// <summary>Shares one <see cref="SemaphoreSlim"/> across all child agents so at most N run in parallel.</summary>
public sealed class ConcurrencyLimitedAgent : DelegatingAIAgent
{
    private readonly SemaphoreSlim _gate;

    /// <summary>Wraps an agent with a shared concurrency gate.</summary>
    public ConcurrencyLimitedAgent(AIAgent innerAgent, SemaphoreSlim gate) : base(innerAgent) => _gate = gate;

    /// <inheritdoc />
    protected override async Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await base.RunCoreAsync(messages, session, options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await foreach (AgentResponseUpdate update in base.RunCoreStreamingAsync(messages, session, options, cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
