using System.ComponentModel.DataAnnotations;

namespace ProcurementCopilot.Application.Configuration;

/// <summary>Security controls bound from the <c>Security</c> section.</summary>
public sealed class SecurityOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Security";

    /// <summary>Tool approval policy.</summary>
    [Required]
    public ApprovalPolicyOptions ApprovalPolicy { get; set; } = new();

    /// <summary>Confined shell settings.</summary>
    [Required]
    public ShellOptions Shell { get; set; } = new();
}

/// <summary>The single source of truth for which tools require human approval.</summary>
public sealed class ApprovalPolicyOptions
{
    /// <summary>Defaults used when configuration does not list any tool.</summary>
    public static readonly IReadOnlyList<string> Defaults = ["draft_clarification_email", "record_award_recommendation", "shell"];

    /// <summary>Tool names that always require approval and can never be auto-approved. Bound from configuration.</summary>
    public IReadOnlyList<string> RequireApprovalFor { get; set; } = [];

    /// <summary>Gets the configured list, or <see cref="Defaults"/> when configuration is empty (fail closed).</summary>
    public IReadOnlyList<string> EffectiveRequireApprovalFor => RequireApprovalFor.Count > 0 ? RequireApprovalFor : Defaults;
}

/// <summary>Confined shell settings.</summary>
public sealed class ShellOptions
{
    /// <summary>Defaults used when configuration does not list any command.</summary>
    public static readonly IReadOnlyList<string> DefaultAllowedCommands =
        ["ls", "dir", "cat", "type", "head", "tail", "wc", "grep", "findstr", "find", "pwd", "echo"];

    /// <summary>Commands the model may run (first token of each pipeline segment). Bound from configuration.</summary>
    public IReadOnlyList<string> AllowedCommands { get; set; } = [];

    /// <summary>Gets the configured allowlist, or <see cref="DefaultAllowedCommands"/> when configuration is empty.</summary>
    public IReadOnlyList<string> EffectiveAllowedCommands => AllowedCommands.Count > 0 ? AllowedCommands : DefaultAllowedCommands;

    /// <summary>Hard per-command timeout.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Maximum captured output.</summary>
    [Range(1024, 10_000_000)]
    public int MaxOutputBytes { get; set; } = 32_768;

    /// <summary>Gets the timeout as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);
}
