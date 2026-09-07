using Microsoft.Extensions.AI;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Agent.Middleware;

/// <summary>
/// Function middleware that blocks side-effecting tools unless the session is in execute mode.
/// This is a security control: plan mode cannot cause side effects even if the model calls the tool.
/// </summary>
public sealed class ModeGuardMiddleware : DelegatingAIFunction
{
    private readonly IAgentModeAccessor _modes;

    /// <summary>Wraps a side-effecting function.</summary>
    public ModeGuardMiddleware(AIFunction innerFunction, IAgentModeAccessor modes) : base(innerFunction) => _modes = modes;

    /// <summary>Wraps a function only when it is side-effecting; read-only functions are returned unchanged.</summary>
    public static AIFunction Apply(AIFunction function, IAgentModeAccessor modes, IReadOnlySet<string> sideEffecting) =>
        sideEffecting.Contains(function.Name) ? new ModeGuardMiddleware(function, modes) : function;

    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        string mode = await _modes.GetCurrentModeAsync(cancellationToken).ConfigureAwait(false);
        Result<string> verdict = ModeGuard.Check(mode, Name);
        if (verdict.IsFailure)
        {
            return ToolError.FromError(verdict.Error);
        }

        return await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
    }
}
