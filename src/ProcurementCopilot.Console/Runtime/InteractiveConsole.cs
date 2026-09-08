using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.ConsoleApp.Hosting;
using ProcurementCopilot.ConsoleApp.Ui;
using ProcurementCopilot.Infrastructure.SqlServer;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>The classic line-oriented loop: banner, prompt, commands, agent turns, observer feed, Ctrl+C handling and clean exit.</summary>
public sealed class InteractiveConsole
{
    private readonly AppState _state;
    private readonly SessionDriver _driver;
    private readonly ClassicPresenter _presenter;
    private readonly ObserverPoller _poller;
    private readonly DataProviderSelection _data;
    private readonly LaunchOptions _launch;
    private bool _observing;

    /// <summary>Initializes the app.</summary>
    public InteractiveConsole(AppState state, SessionDriver driver, ClassicPresenter presenter, ObserverPoller poller, DataProviderSelection data, LaunchOptions launch)
    {
        _state = state;
        _driver = driver;
        _presenter = presenter;
        _poller = poller;
        _data = data;
        _launch = launch;
    }

    /// <summary>Runs until the user exits. Returns the process exit code.</summary>
    public async Task<int> RunAsync()
    {
        string? notice = await _driver.StartAsync(_state, _launch).ConfigureAwait(false);
        Console.CancelKeyPress += OnCancelKeyPress;
        Render.Banner(_state.FakeMode, SessionIdentity.GetOrCreate(_state.Session), _data.Provider, _data.Description);
        if (_data.Warning is not null)
        {
            Render.Warn(_data.Warning);
        }

        if (notice is not null)
        {
            Render.Warn(notice);
        }

        _poller.Changed += OnObserved;
        SyncObserver();
        while (!_state.ExitRequested)
        {
            Render.Prompt(await _state.GetModeAsync().ConfigureAwait(false), _state.Role == SessionRole.Observer);
            string? input = Console.ReadLine();
            if (input is null)
            {
                break;
            }

            await _driver.HandleAsync(_state, _presenter, input).ConfigureAwait(false);
            SyncObserver();
        }

        _poller.Stop();
        await _driver.ShutdownAsync(_state).ConfigureAwait(false);
        Render.Info(_state.Role == SessionRole.Driver ? "Session saved. Goodbye." : "Detached from the observed session. Goodbye.");
        return 0;
    }

    private void SyncObserver()
    {
        bool shouldObserve = _state.Role == SessionRole.Observer;
        if (shouldObserve && !_observing)
        {
            _poller.Start(_state, TimeSpan.FromSeconds(1));
            Render.Info("Following the driver's status file; tool calls and activity changes appear here as they happen.");
        }
        else if (!shouldObserve && _observing)
        {
            _poller.Stop();
        }

        _observing = shouldObserve;
    }

    private void OnObserved(ObserverUpdate update)
    {
        if (update.ActivityChanged)
        {
            string tool = update.Status.CurrentTool is null ? string.Empty : $" ({update.Status.CurrentTool})";
            Render.Info($"[driver {update.Status.Instance}] {update.Status.Activity}{tool}, mode {update.Status.Mode}");
        }

        foreach (ToolEvent tool in update.NewTools)
        {
            Render.ToolCall(tool.Name, tool.Arguments, tool.Result);
        }

        if (update.SessionReloaded && update.NewTools.Count == 0 && !update.ActivityChanged)
        {
            Render.Info("[driver] session file updated (todos, history and evaluation state re-read).");
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        if (SessionDriver.CancelRun(_state))
        {
            Render.Warn("\nCancelling the current run (press Ctrl+C again to exit)…");
            return;
        }

        Render.Info("\nExiting…");
        _poller.Stop();
        _driver.ShutdownAsync(_state).GetAwaiter().GetResult();
        Environment.Exit(0);
    }
}
