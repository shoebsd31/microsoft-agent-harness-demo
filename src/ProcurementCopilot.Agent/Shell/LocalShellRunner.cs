using Microsoft.Agents.AI.Tools.Shell;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Application.Security;

namespace ProcurementCopilot.Agent.Shell;

/// <summary>
/// <see cref="IShellExecutor"/> over the package <see cref="LocalShellExecutor"/>: stateless mode, working directory locked
/// to the workspace root, clean environment, hard timeout and output cap. The allowlist is enforced by
/// <see cref="ShellCommandPolicy"/> before this class is ever called; the executor's own <see cref="ShellPolicy"/>
/// re-checks it as a second layer.
/// </summary>
public sealed class LocalShellRunner : IShellExecutor, IAsyncDisposable
{
    private readonly WorkspacePathPolicy _paths;
    private readonly ShellCommandPolicy _policy;
    private readonly ShellOptions _options;
    private readonly ShellSelection _selection;
    private readonly SemaphoreSlim _init = new(1, 1);
    private LocalShellExecutor? _executor;

    /// <summary>Initializes the runner.</summary>
    public LocalShellRunner(WorkspacePathPolicy paths, ShellCommandPolicy policy, ShellOptions options)
    {
        _paths = paths;
        _policy = policy;
        _options = options;
        _selection = ShellSelector.Resolve();
    }

    /// <inheritdoc />
    public string ShellFamily => _selection.Family;

    /// <inheritdoc />
    public async Task<ShellExecution> RunAsync(string command, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        LocalShellExecutor executor = await GetExecutorAsync(cancellationToken).ConfigureAwait(false);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout + TimeSpan.FromSeconds(2));
        try
        {
            ShellResult result = await executor.RunAsync(command, cts.Token).ConfigureAwait(false);
            return new ShellExecution(result.Stdout, result.Stderr, result.ExitCode, result.TimedOut, result.Truncated);
        }
        catch (ShellCommandRejectedException ex)
        {
            return new ShellExecution(string.Empty, "rejected by shell policy: " + ex.Message, 126, false, false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ShellExecution(string.Empty, "timed out", 124, true, false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_executor is not null)
        {
            await _executor.DisposeAsync().ConfigureAwait(false);
        }

        _init.Dispose();
    }

    private async Task<LocalShellExecutor> GetExecutorAsync(CancellationToken cancellationToken)
    {
        if (_executor is not null)
        {
            return _executor;
        }

        await _init.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_executor is null)
            {
                Directory.CreateDirectory(_paths.Root);
                var executor = new LocalShellExecutor(new LocalShellExecutorOptions
                {
                    Mode = ShellMode.Stateless,
                    WorkingDirectory = _paths.Root,
                    ConfineWorkingDirectory = true,
                    ShellArgv = _selection.Argv,
                    Timeout = _options.Timeout,
                    MaxOutputBytes = _options.MaxOutputBytes,
                    Policy = new ShellPolicy(custom: request => _policy.Evaluate(request.Command) is { IsAllowed: false } v ? ShellPolicyOutcome.Deny(v.Reason) : null),
                });
                await executor.InitializeAsync(cancellationToken).ConfigureAwait(false);
                _executor = executor;
            }

            return _executor;
        }
        finally
        {
            _init.Release();
        }
    }
}
