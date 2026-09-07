namespace ProcurementCopilot.Application.Security;

/// <summary>Specific reasons a shell command line is rejected. Each maps to one table-driven test group.</summary>
public enum ShellPolicyCode
{
    /// <summary>The command is allowed.</summary>
    Allowed,

    /// <summary>Empty or whitespace-only command line.</summary>
    EmptyCommand,

    /// <summary>The command name is not on the allowlist.</summary>
    CommandNotAllowed,

    /// <summary>Contains <c>;</c>, <c>&amp;&amp;</c>, <c>||</c>, <c>&amp;</c> or a newline.</summary>
    ChainingNotAllowed,

    /// <summary>Contains <c>&gt;</c>, <c>&gt;&gt;</c> or <c>&lt;</c>.</summary>
    RedirectionNotAllowed,

    /// <summary>Contains backticks or <c>$(</c>.</summary>
    SubstitutionNotAllowed,

    /// <summary>Contains <c>$VAR</c>, <c>${VAR}</c> or <c>%VAR%</c>.</summary>
    VariableExpansionNotAllowed,

    /// <summary>An argument is an absolute path.</summary>
    AbsolutePathNotAllowed,

    /// <summary>An argument is a UNC path.</summary>
    UncPathNotAllowed,

    /// <summary>An argument contains a <c>..</c> segment.</summary>
    ParentTraversalNotAllowed,

    /// <summary>A network command such as <c>curl</c> was used.</summary>
    NetworkNotAllowed,

    /// <summary>An argument looks like a URL.</summary>
    UrlNotAllowed,

    /// <summary>A <c>find</c> option other than name filters was used.</summary>
    FindOptionNotAllowed,

    /// <summary>An argument resolves outside the workspace.</summary>
    PathOutsideWorkspace,

    /// <summary>A pipeline segment is empty (for example a trailing pipe).</summary>
    EmptyPipelineSegment,

    /// <summary>The command line exceeds the length cap.</summary>
    TooLong,
}

/// <summary>Outcome of evaluating a command line.</summary>
/// <param name="Code">The verdict code.</param>
/// <param name="Reason">Human-readable reason.</param>
/// <param name="Segments">Parsed segments when allowed.</param>
public sealed record ShellVerdict(ShellPolicyCode Code, string Reason, IReadOnlyList<ShellSegment> Segments)
{
    /// <summary>Gets a value indicating whether the command may run.</summary>
    public bool IsAllowed => Code == ShellPolicyCode.Allowed;
}
