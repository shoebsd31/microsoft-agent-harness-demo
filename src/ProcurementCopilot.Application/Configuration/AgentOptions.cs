using System.ComponentModel.DataAnnotations;

namespace ProcurementCopilot.Application.Configuration;

/// <summary>Harness composition settings bound from the <c>Agent</c> section.</summary>
public sealed class AgentOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Agent";

    /// <summary>Model context window used for compaction.</summary>
    [Range(2_000, 10_000_000)]
    public int MaxContextWindowTokens { get; set; } = 128_000;

    /// <summary>Maximum output tokens per response.</summary>
    [Range(256, 1_000_000)]
    public int MaxOutputTokens { get; set; } = 16_384;

    /// <summary>Per-request function-invocation iteration limit (harness <c>MaximumIterationsPerRequest</c>).</summary>
    [Range(1, 100)]
    public int MaxFunctionInvocationIterations { get; set; } = 15;

    /// <summary>Upper bound on loop re-invocations.</summary>
    [Range(1, 50)]
    public int MaxLoopIterations { get; set; } = 5;

    /// <summary>Add the hosted web-search tool when the chat client supports it.</summary>
    public bool EnableWebSearch { get; set; } = true;

    /// <summary>Reserved: local URL fetching is not implemented and must stay off.</summary>
    public bool EnableLocalWebFetch { get; set; }

    /// <summary>Add the AI judge loop evaluator (sends conversation content to a second model call).</summary>
    public bool EnableJudge { get; set; }

    /// <summary>Organisation named in the prompts. Empty = derived from the data backend (Adventure Works Cycles for SQL Server, Contoso Industrial Systems for JSON).</summary>
    public string OrganisationName { get; set; } = string.Empty;

    /// <summary>Background (child) agent limits.</summary>
    [Required]
    public BackgroundAgentsOptions BackgroundAgents { get; set; } = new();
}

/// <summary>Bounds for background child agents.</summary>
public sealed class BackgroundAgentsOptions
{
    /// <summary>Maximum child agents running concurrently.</summary>
    [Range(1, 16)]
    public int MaxParallel { get; set; } = 2;

    /// <summary>Per-child run timeout in seconds.</summary>
    [Range(1, 3600)]
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Gets the timeout as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);
}
