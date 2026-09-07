namespace ProcurementCopilot.Application.Abstractions;

/// <summary>Well-known agent mode names.</summary>
public static class AgentModes
{
    /// <summary>Interactive planning; side effects are blocked.</summary>
    public const string Plan = "plan";

    /// <summary>Autonomous execution; side effects require approval.</summary>
    public const string Execute = "execute";
}

/// <summary>Reads the operating mode of the session that is currently running.</summary>
public interface IAgentModeAccessor
{
    /// <summary>Returns the current mode, or <see cref="AgentModes.Plan"/> when unknown (fail closed).</summary>
    ValueTask<string> GetCurrentModeAsync(CancellationToken cancellationToken = default);
}
