using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ProcurementCopilot.Agent.Background;

/// <summary>Bounds every run of a child agent with a hard timeout. A timed-out run fails with <see cref="TimeoutException"/>.</summary>
public sealed class TimeoutAgent : DelegatingAIAgent
{
    private readonly TimeSpan _timeout;

    /// <summary>Wraps an agent with a per-run timeout.</summary>
    public TimeoutAgent(AIAgent innerAgent, TimeSpan timeout) : base(innerAgent) => _timeout = timeout;

    /// <summary>Gets the timeout.</summary>
    public TimeSpan Timeout => _timeout;

    /// <inheritdoc />
    protected override async Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);
        try
        {
            return await base.RunCoreAsync(messages, session, options, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Background agent '{Name}' exceeded its {_timeout.TotalSeconds:0}s timeout.");
        }
    }

    /// <inheritdoc />
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);
        IAsyncEnumerator<AgentResponseUpdate> enumerator = base.RunCoreStreamingAsync(messages, session, options, cts.Token).GetAsyncEnumerator(cts.Token);
        try
        {
            while (true)
            {
                bool moved;
                try
                {
                    moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"Background agent '{Name}' exceeded its {_timeout.TotalSeconds:0}s timeout.");
                }

                if (!moved)
                {
                    yield break;
                }

                yield return enumerator.Current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }
}
