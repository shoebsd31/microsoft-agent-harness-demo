using Microsoft.Extensions.Options;
using ProcurementCopilot.Application.Configuration;

namespace ProcurementCopilot.Agent;

/// <summary>Everything <see cref="HarnessAgentFactory"/> needs to compose the harness, resolved from configuration.</summary>
public sealed record ProcurementAgentOptions
{
    /// <summary>Harness limits and switches.</summary>
    public AgentOptions Agent { get; init; } = new();

    /// <summary>Approval and shell policy.</summary>
    public SecurityOptions Security { get; init; } = new();

    /// <summary>Workspace confinement.</summary>
    public WorkspaceOptions Workspace { get; init; } = new();

    /// <summary>Absolute skills directory (defaults to <c>{BaseDirectory}/skills</c>).</summary>
    public string SkillsPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "skills");

    /// <summary>Absolute root for session file memory (defaults to <c>{workspace}/agent-file-memory</c>).</summary>
    public string? FileMemoryRoot { get; init; }

    /// <summary>Whether to checkpoint the session to the session store after every model call.</summary>
    public bool PersistSessions { get; init; } = true;

    /// <summary>Builds options from the bound configuration sections.</summary>
    public static ProcurementAgentOptions FromConfiguration(IOptions<AgentOptions> agent, IOptions<SecurityOptions> security, IOptions<WorkspaceOptions> workspace) =>
        new() { Agent = agent.Value, Security = security.Value, Workspace = workspace.Value };
}
