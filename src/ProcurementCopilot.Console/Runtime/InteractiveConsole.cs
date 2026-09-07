using Microsoft.Extensions.Logging;
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Agent.State;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.ConsoleApp.Commands;
using ProcurementCopilot.ConsoleApp.Hosting;
using ProcurementCopilot.ConsoleApp.Ui;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>The interactive loop: banner, prompt, commands, agent turns, todo panel, cancellation and clean exit.</summary>
public sealed class InteractiveConsole
{
    private readonly AppState _state;
    private readonly AgentBootstrapper _bootstrapper;
    private readonly AgentTurnRunner _runner;
    private readonly CommandDispatcher _commands;
    private readonly ISessionStore _sessions;
    private readonly LaunchOptions _launch;
    private readonly ILogger<InteractiveConsole> _logger;

    /// <summary>Initializes the app.</summary>
    public InteractiveConsole(AppState state, AgentBootstrapper bootstrapper, AgentTurnRunner runner, CommandDispatcher commands, ISessionStore sessions, LaunchOptions launch, ILogger<InteractiveConsole> logger)
    {
        _state = state;
        _bootstrapper = bootstrapper;
        _runner = runner;
        _commands = commands;
        _sessions = sessions;
        _launch = launch;
        _logger = logger;
    }

    /// <summary>Runs until the user exits. Returns the process exit code.</summary>
    public async Task<int> RunAsync()
    {
        await _bootstrapper.InitializeAsync(_state, _launch).ConfigureAwait(false);
        Console.CancelKeyPress += OnCancelKeyPress;
        Render.Banner(_state.FakeMode, SessionIdentity.GetOrCreate(_state.Session));

        while (!_state.ExitRequested)
        {
            Render.Prompt(await _state.GetModeAsync().ConfigureAwait(false));
            string? input = Console.ReadLine();
            if (input is null)
            {
                break;
            }

            input = input.Trim();
            if (input.Length == 0)
            {
                continue;
            }

            if (input.StartsWith('/'))
            {
                await _commands.DispatchAsync(input, _state).ConfigureAwait(false);
                continue;
            }

            await RunTurnAsync(input).ConfigureAwait(false);
        }

        await FlushAsync().ConfigureAwait(false);
        Render.Info("Session saved. Goodbye.");
        return 0;
    }

    private async Task RunTurnAsync(string input)
    {
        using var cts = new CancellationTokenSource();
        _state.RunCancellation = cts;
        try
        {
            await _runner.RunTurnAsync(_state, input, cts.Token).ConfigureAwait(false);
        }
        finally
        {
            _state.RunCancellation = null;
        }

        await AfterTurnAsync(input).ConfigureAwait(false);
    }

    private async Task AfterTurnAsync(string input)
    {
        if (_state.Todos is { } todos)
        {
            Render.Todos(await todos.GetAllTodosAsync(_state.Session).ConfigureAwait(false));
        }

        if (_state.Factory.LoopEvaluator is { } evaluator)
        {
            IReadOnlyList<string> outstanding = await evaluator.OutstandingAsync(_state.Session).ConfigureAwait(false);
            if (outstanding.Count > 0 && string.Equals(await _state.GetModeAsync().ConfigureAwait(false), AgentModes.Execute, StringComparison.OrdinalIgnoreCase))
            {
                Render.Warn("Loop budget exhausted or run interrupted. Still outstanding: " + string.Join("; ", outstanding));
            }
        }

        using (SessionEvaluationStateStore.UseSession(_state.Session))
        {
            await SessionPersistence.SaveAsync(_state.Agent, _state.Session, _sessions, input).ConfigureAwait(false);
        }
    }

    private async Task FlushAsync()
    {
        try
        {
            await SessionPersistence.SaveAsync(_state.Agent, _state.Session, _sessions, null).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not flush the session on exit");
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        if (_state.RunCancellation is { IsCancellationRequested: false } running)
        {
            running.Cancel();
            Render.Warn("\nCancelling the current run (press Ctrl+C again to exit)…");
            return;
        }

        Render.Info("\nExiting…");
        FlushAsync().GetAwaiter().GetResult();
        Environment.Exit(0);
    }
}
