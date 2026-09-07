using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Application.Security;

/// <summary>Pure decision: may a side-effecting tool run in the current mode?</summary>
public static class ModeGuard
{
    /// <summary>Error code returned when a side effect is attempted in plan mode.</summary>
    public const string BlockedCode = "Mode.SideEffectBlocked";

    /// <summary>Returns success when the mode allows side effects, otherwise a failure telling the model how to proceed.</summary>
    public static Result<string> Check(string? currentMode, string toolName) =>
        string.Equals(currentMode, AgentModes.Execute, StringComparison.OrdinalIgnoreCase)
            ? currentMode!
            : Error.Validation(BlockedCode, $"Tool '{toolName}' has side effects and is blocked in '{currentMode ?? AgentModes.Plan}' mode. Present your plan and ask the analyst to switch to execute mode (/mode execute) before calling it again.");
}
