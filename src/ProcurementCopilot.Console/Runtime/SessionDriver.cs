using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.ConsoleApp.Commands;
using ProcurementCopilot.ConsoleApp.Hosting;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>
/// Front-end independent input handling: slash commands go to the dispatcher, prompts run an agent turn (drivers only),
/// and every turn ends with status publishing and a session save. Both the classic loop and the TUI call this.
/// </summary>
public sealed class SessionDriver
{
    private static readonly HashSet<string> BlockedWhileRunning = new(StringComparer.OrdinalIgnoreCase) { "/session", "/mode", "/approvals", "/takeover", "/detach", "/exit" };

    private readonly AgentBootstrapper _bootstrapper;
    private readonly SessionCoordinator _coordinator;
    private readonly AgentTurnRunner _runner;
    private readonly CommandDispatcher _commands;
    private readonly ILogger<SessionDriver> _logger;

    /// <summary>Initializes the driver.</summary>
    public SessionDriver(AgentBootstrapper bootstrapper, SessionCoordinator coordinator, AgentTurnRunner runner, CommandDispatcher commands, ILogger<SessionDriver> logger)
    {
        _bootstrapper = bootstrapper;
        _coordinator = coordinator;
        _runner = runner;
        _commands = commands;
        _logger = logger;
    }

    /// <summary>Builds the agent and session, then attaches or takes the lock according to the launch flags. Returns a start-up notice.</summary>
    public async Task<string?> StartAsync(AppState state, LaunchOptions launch)
    {
        await _bootstrapper.InitializeAsync(state, launch).ConfigureAwait(false);
        if (launch.AttachSessionId is { } observed)
        {
            if (!await _coordinator.AttachAsync(state, observed).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Session not found: " + observed.ToString("N"));
            }

            SessionLockInfo? owner = _coordinator.LiveOwner(observed);
            return owner is null
                ? "Observing session " + observed.ToString("N") + ". Nobody drives it right now; type /takeover to drive it."
                : $"Observing session {observed:N}, driven by {owner.Instance} since {owner.AcquiredAt.ToLocalTime():HH:mm:ss}. Prompts are read-only here; /takeover once the driver exits or detaches.";
        }

        SessionLockInfo? holder = await _coordinator.TryDriveAsync(state).ConfigureAwait(false);
        return holder is null
            ? null
            : $"Session {SessionIdentity.GetOrCreate(state.Session):N} is already driven by {holder.Instance}. Attached as an observer instead; /takeover once it is free.";
    }

    /// <summary>Handles one input line. Returns false when the line was refused (observer prompt or blocked command).</summary>
    public async Task<bool> HandleAsync(AppState state, ITurnPresenter presenter, string line)
    {
        line = line.Trim();
        if (line.Length == 0)
        {
            return true;
        }

        if (line.StartsWith('/'))
        {
            string name = line.Split(' ', 2)[0];
            if (state.IsRunning && BlockedWhileRunning.Contains(name))
            {
                presenter.Warn($"{name} is not available while a run is in progress (Esc / Ctrl+C cancels the run).");
                return false;
            }

            await _commands.DispatchAsync(line, state).ConfigureAwait(false);
            return true;
        }

        if (state.Role == SessionRole.Observer)
        {
            SessionLockInfo? owner = _coordinator.LiveOwner(SessionIdentity.GetOrCreate(state.Session));
            presenter.Warn(owner is null
                ? "This instance only observes the session. Type /takeover to drive it."
                : $"This session is driven by {owner.Instance}; prompts can only be sent from there. Watch here, or /takeover when it is free.");
            return false;
        }

        if (state.IsRunning)
        {
            presenter.Warn("A run is already in progress. Wait for it to finish or cancel it first.");
            return false;
        }

        await RunPromptAsync(state, presenter, line).ConfigureAwait(false);
        return true;
    }

    /// <summary>Runs one agent turn, then publishes status, shows todos and saves the session.</summary>
    public async Task RunPromptAsync(AppState state, ITurnPresenter presenter, string prompt)
    {
        using var cts = new CancellationTokenSource();
        state.RunCancellation = cts;
        try
        {
            await _runner.RunTurnAsync(state, presenter, prompt, cts.Token).ConfigureAwait(false);
        }
        finally
        {
            state.RunCancellation = null;
        }

        IReadOnlyList<TodoItem> todos = state.Todos is { } provider ? await provider.GetAllTodosAsync(state.Session).ConfigureAwait(false) : [];
        IReadOnlyList<string> outstanding = state.Factory.LoopEvaluator is { } evaluator ? await evaluator.OutstandingAsync(state.Session).ConfigureAwait(false) : [];
        bool execute = string.Equals(await state.GetModeAsync().ConfigureAwait(false), AgentModes.Execute, StringComparison.OrdinalIgnoreCase);
        presenter.TurnCompleted(todos, execute ? outstanding : []);
        await _coordinator.CompleteTurnAsync(state, prompt, outstanding).ConfigureAwait(false);
    }

    /// <summary>Cancels the run in progress, if any. Returns true when something was cancelled.</summary>
    public static bool CancelRun(AppState state)
    {
        if (state.RunCancellation is { IsCancellationRequested: false } running)
        {
            running.Cancel();
            return true;
        }

        return false;
    }

    /// <summary>Saves (drivers only), publishes the exit and releases the lock.</summary>
    public async Task ShutdownAsync(AppState state)
    {
        try
        {
            await _coordinator.SaveAsync(state, null).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not flush the session on exit");
        }

        _coordinator.Exit();
    }
}
