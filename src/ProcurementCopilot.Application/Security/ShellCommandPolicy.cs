using System.Text.RegularExpressions;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Application.Security;

/// <summary>
/// Allowlist policy evaluated on the parsed command line before anything reaches a shell.
/// Denies chaining, redirection, substitution, variable expansion, absolute/UNC/parent paths,
/// network commands and URL-shaped arguments; confines every path argument to the workspace.
/// </summary>
public sealed partial class ShellCommandPolicy
{
    /// <summary>Maximum accepted command-line length.</summary>
    public const int MaxLength = 1_000;

    private static readonly HashSet<string> NetworkCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "curl", "wget", "invoke-webrequest", "iwr", "invoke-restmethod", "irm", "nc", "ncat", "netcat", "ssh", "scp", "sftp", "ftp", "telnet", "ping", "nslookup", "git",
    };

    private static readonly HashSet<string> FindAllowedOptions = new(StringComparer.Ordinal)
    {
        "-name", "-iname", "-type", "-maxdepth", "-mindepth", "-path", "-ipath",
    };

    private readonly HashSet<string> _allowed;
    private readonly WorkspacePathPolicy _paths;

    /// <summary>Initializes the policy from configuration and the workspace confinement rule.</summary>
    public ShellCommandPolicy(ShellOptions options, WorkspacePathPolicy paths)
    {
        _allowed = new HashSet<string>(options.EffectiveAllowedCommands, StringComparer.OrdinalIgnoreCase);
        _paths = paths;
    }

    /// <summary>Evaluates a command line.</summary>
    public ShellVerdict Evaluate(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return Deny(ShellPolicyCode.EmptyCommand, "No command was given.");
        }

        if (commandLine.Length > MaxLength)
        {
            return Deny(ShellPolicyCode.TooLong, $"Command lines longer than {MaxLength} characters are rejected.");
        }

        ShellVerdict? raw = CheckRawText(commandLine);
        if (raw is not null)
        {
            return raw;
        }

        IReadOnlyList<ShellSegment> segments = ShellCommandTokenizer.Tokenize(commandLine);
        foreach (ShellSegment segment in segments)
        {
            ShellVerdict? verdict = CheckSegment(segment);
            if (verdict is not null)
            {
                return verdict;
            }
        }

        return new ShellVerdict(ShellPolicyCode.Allowed, "Allowed.", segments);
    }

    private static ShellVerdict? CheckRawText(string text)
    {
        if (text.Contains(';', StringComparison.Ordinal) || text.Contains("&&", StringComparison.Ordinal) ||
            text.Contains("||", StringComparison.Ordinal) || text.Contains('&', StringComparison.Ordinal) ||
            text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal))
        {
            return Deny(ShellPolicyCode.ChainingNotAllowed, "Command chaining (; && || & newline) is not allowed.");
        }

        if (text.Contains('>', StringComparison.Ordinal) || text.Contains('<', StringComparison.Ordinal))
        {
            return Deny(ShellPolicyCode.RedirectionNotAllowed, "Redirection (> >> <) is not allowed.");
        }

        if (text.Contains('`', StringComparison.Ordinal) || text.Contains("$(", StringComparison.Ordinal))
        {
            return Deny(ShellPolicyCode.SubstitutionNotAllowed, "Command substitution (backticks, $( )) is not allowed.");
        }

        if (VariableExpansion().IsMatch(text))
        {
            return Deny(ShellPolicyCode.VariableExpansionNotAllowed, "Environment variable expansion ($VAR, ${VAR}, %VAR%) is not allowed.");
        }

        return null;
    }

    private ShellVerdict? CheckSegment(ShellSegment segment)
    {
        if (segment.Command.Length == 0)
        {
            return Deny(ShellPolicyCode.EmptyPipelineSegment, "Empty pipeline segment.");
        }

        if (NetworkCommands.Contains(segment.Command))
        {
            return Deny(ShellPolicyCode.NetworkNotAllowed, $"Network command '{segment.Command}' is not allowed.");
        }

        if (!_allowed.Contains(segment.Command))
        {
            return Deny(ShellPolicyCode.CommandNotAllowed, $"Command '{segment.Command}' is not on the allowlist ({string.Join(", ", _allowed.Order(StringComparer.Ordinal))}).");
        }

        foreach (string argument in segment.Arguments)
        {
            ShellVerdict? verdict = CheckArgument(segment.Command, argument);
            if (verdict is not null)
            {
                return verdict;
            }
        }

        return null;
    }

    private ShellVerdict? CheckArgument(string command, string argument)
    {
        if (IsWindowsSwitch(command, argument))
        {
            return null;
        }

        if (UrlShaped().IsMatch(argument))
        {
            return Deny(ShellPolicyCode.UrlNotAllowed, "URL-shaped arguments are not allowed.");
        }

        if (argument.StartsWith(@"\\", StringComparison.Ordinal) || argument.StartsWith("//", StringComparison.Ordinal))
        {
            return Deny(ShellPolicyCode.UncPathNotAllowed, "UNC paths are not allowed.");
        }

        if (argument.StartsWith('/') || argument.StartsWith('\\') || (argument.Length > 1 && argument[1] == ':') || argument.StartsWith('~'))
        {
            return Deny(ShellPolicyCode.AbsolutePathNotAllowed, "Absolute paths are not allowed; use workspace-relative paths.");
        }

        if (argument.Split('/', '\\').Contains(".."))
        {
            return Deny(ShellPolicyCode.ParentTraversalNotAllowed, "Parent traversal ('..') is not allowed.");
        }

        if (string.Equals(command, "find", StringComparison.OrdinalIgnoreCase) && argument.StartsWith('-') && !FindAllowedOptions.Contains(argument))
        {
            return Deny(ShellPolicyCode.FindOptionNotAllowed, $"find option '{argument}' is not allowed; only name/type/depth filters are.");
        }

        if (!argument.StartsWith('-') && LooksLikePath(argument))
        {
            Result<string> resolved = _paths.Resolve(argument, PathAccess.Read);
            if (resolved.IsFailure && resolved.Error.Code is "Path.OutsideRoot" or "Path.Symlink")
            {
                return Deny(ShellPolicyCode.PathOutsideWorkspace, resolved.Error.Message);
            }
        }

        return null;
    }

    private static bool IsWindowsSwitch(string command, string argument) =>
        command.ToLowerInvariant() is "findstr" or "dir" or "type" && WindowsSwitch().IsMatch(argument);

    private static bool LooksLikePath(string argument) =>
        argument.Contains('/', StringComparison.Ordinal) || argument.Contains('\\', StringComparison.Ordinal) || argument.Contains('.', StringComparison.Ordinal);

    private static ShellVerdict Deny(ShellPolicyCode code, string reason) => new(code, reason, []);

    [GeneratedRegex(@"^/[A-Za-z]{1,2}(:[A-Za-z0-9]*)?$")]
    private static partial Regex WindowsSwitch();

    [GeneratedRegex(@"\$\{|\$[A-Za-z_]|%[A-Za-z_][A-Za-z0-9_]*%")]
    private static partial Regex VariableExpansion();

    [GeneratedRegex(@"://|^www\.|^[a-z0-9.-]+\.(com|net|org|io|dev|gov|edu)(/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex UrlShaped();
}
