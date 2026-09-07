using Microsoft.Agents.AI;
using ProcurementCopilot.Application.Abstractions;

namespace ProcurementCopilot.Agent.Middleware;

/// <summary>Reads the mode of the running session via <see cref="AIAgent.CurrentRunContext"/> and the harness <see cref="AgentModeProvider"/>. Fails closed to plan mode.</summary>
public sealed class RunContextModeAccessor : IAgentModeAccessor
{
    /// <inheritdoc />
    public async ValueTask<string> GetCurrentModeAsync(CancellationToken cancellationToken = default)
    {
        AgentRunContext? context = AIAgent.CurrentRunContext;
        AgentModeProvider? provider = context?.Agent.GetService<AgentModeProvider>();
        if (context?.Session is null || provider is null)
        {
            return AgentModes.Plan;
        }

        return await provider.GetModeAsync(context.Session, cancellationToken).ConfigureAwait(false);
    }
}
