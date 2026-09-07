namespace ProcurementCopilot.Application.Abstractions;

/// <summary>Result of running a shell command.</summary>
/// <param name="Stdout">Captured standard output.</param>
/// <param name="Stderr">Captured standard error.</param>
/// <param name="ExitCode">Process exit code.</param>
/// <param name="TimedOut">Whether the hard timeout fired.</param>
/// <param name="Truncated">Whether output was cut to the configured cap.</param>
public sealed record ShellExecution(string Stdout, string Stderr, int ExitCode, bool TimedOut, bool Truncated);

/// <summary>Runs an already policy-approved command line inside the workspace.</summary>
public interface IShellExecutor
{
    /// <summary>Gets the shell family in use, for example <c>posix</c> or <c>powershell</c>.</summary>
    string ShellFamily { get; }

    /// <summary>Runs the command with the given timeout.</summary>
    Task<ShellExecution> RunAsync(string command, TimeSpan timeout, CancellationToken cancellationToken = default);
}
