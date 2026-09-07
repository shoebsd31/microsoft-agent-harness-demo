using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent.Tools;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Agent.Shell;

/// <summary>
/// The <c>shell</c> tool: evaluates <see cref="ShellCommandPolicy"/> on the parsed command line, then runs it through the
/// confined executor with a hard timeout and an output cap. Every call requires approval (it is in <c>RequireApprovalFor</c>).
/// </summary>
public sealed class ConfinedShellTool
{
    private readonly ShellCommandPolicy _policy;
    private readonly IShellExecutor _executor;
    private readonly ShellOptions _options;
    private readonly IAuditLog _audit;
    private readonly IClock _clock;

    /// <summary>Initializes the tool.</summary>
    public ConfinedShellTool(ShellCommandPolicy policy, IShellExecutor executor, ShellOptions options, IAuditLog audit, IClock clock)
    {
        _policy = policy;
        _executor = executor;
        _options = options;
        _audit = audit;
        _clock = clock;
    }

    /// <summary>Creates the <see cref="AIFunction"/> named <c>shell</c> (unwrapped; the toolset adds the approval wrapper).</summary>
    public AIFunction Create() => AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
    {
        Name = ToolNames.Shell,
        Description = $"Run one read-only shell command inside the workspace folder ({_executor.ShellFamily} shell). Allowed commands: {string.Join(", ", _options.EffectiveAllowedCommands)}. " +
                      "Pipes between allowed commands are fine; chaining, redirection, substitution, variables, absolute paths and '..' are rejected. Paths are relative to the workspace (e.g. rfps/RFP-2026-017). Output is capped and the command times out after " + _options.TimeoutSeconds + "s.",
    });

    /// <summary>Executes the tool.</summary>
    [Description("Run one read-only shell command inside the workspace.")]
    public async Task<string> ExecuteAsync(
        [Description("The command line, e.g. 'ls rfps/RFP-2026-017' or 'wc -l rfps/RFP-2026-017/bids/BID-001-mueller.md'.")] string command,
        CancellationToken cancellationToken)
    {
        ShellVerdict verdict = _policy.Evaluate(command);
        string hash = ArgumentHasher.Hash(command);
        if (!verdict.IsAllowed)
        {
            await _audit.RecordActionAsync(new ActionRecord(_clock.UtcNow, ToolNames.Shell, hash, "denied", verdict.Code.ToString()), cancellationToken).ConfigureAwait(false);
            return ToolError.FromError(Error.Validation("Shell." + verdict.Code, verdict.Reason));
        }

        ShellExecution run = await _executor.RunAsync(command, _options.Timeout, cancellationToken).ConfigureAwait(false);
        await _audit.RecordActionAsync(new ActionRecord(_clock.UtcNow, ToolNames.Shell, hash, run.TimedOut ? "timeout" : "exit:" + run.ExitCode, null), cancellationToken).ConfigureAwait(false);
        if (run.TimedOut)
        {
            return ToolError.FromError(Error.Validation("Shell.Timeout", $"The command exceeded the {_options.TimeoutSeconds}s limit and was terminated."));
        }

        return Format(run);
    }

    private string Format(ShellExecution run)
    {
        var sb = new StringBuilder();
        (string stdout, bool truncatedOut) = Cap(run.Stdout);
        (string stderr, bool truncatedErr) = Cap(run.Stderr);
        sb.Append("exit_code: ").Append(run.ExitCode).AppendLine();
        sb.AppendLine("stdout:").AppendLine(stdout);
        if (stderr.Length > 0)
        {
            sb.AppendLine("stderr:").AppendLine(stderr);
        }

        if (run.Truncated || truncatedOut || truncatedErr)
        {
            sb.Append("note: output truncated to ").Append(_options.MaxOutputBytes).AppendLine(" bytes.");
        }

        return sb.ToString().TrimEnd();
    }

    private (string Text, bool Truncated) Cap(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) <= _options.MaxOutputBytes)
        {
            return (text, false);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        return (Encoding.UTF8.GetString(bytes, 0, _options.MaxOutputBytes), true);
    }
}
