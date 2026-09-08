#pragma warning disable CS0618 // Terminal.Gui 2.4.17 flags TextView and the static Application facade obsolete ahead of v3; both are the stable, documented API of this release.
using Microsoft.Extensions.Logging;
using ProcurementCopilot.Agent;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.ConsoleApp.Hosting;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.ConsoleApp.Ui;
using ProcurementCopilot.Infrastructure.SqlServer;
using ProcurementCopilot.Infrastructure.Telemetry;
using TgApp = Terminal.Gui.App.Application;

namespace ProcurementCopilot.ConsoleApp.Tui;

/// <summary>Full-screen Terminal.Gui front-end: transcript, prompt, live side panels, approval dialog, observer feed and key bindings.</summary>
public sealed class TuiShell(AppState state, SessionDriver driver, ApprovalPrompt approvals, ObserverPoller poller, StatusPublisher status, SpanRingBuffer spans, ProcurementAgentOptions options, DataProviderSelection data, LaunchOptions launch, ILogger<TuiShell> logger)
{
    private readonly PromptHistory _history = new();
    private TuiLayout _layout = null!;
    private TuiPresenter _presenter = null!;
    private TranscriptPane _pane = null!;
    private TuiPanels _panels = null!;
    private Action _afterInput = () => { };

    /// <summary>Runs the TUI until the user quits. Returns the process exit code.</summary>
    public async Task<int> RunAsync()
    {
        string? notice = await driver.StartAsync(state, launch).ConfigureAwait(false);
        string mode = await state.GetModeAsync().ConfigureAwait(false);
        TgApp.Init();
        SpectreCapture capture = new((int)(Math.Max(80, Console.WindowWidth) * 0.64) - 4);
        TuiObserverFeed? feed = null;
        try
        {
            Build(mode);
            feed = new TuiObserverFeed(state, poller, _presenter, _panels);
            TuiObserverFeed.Welcome(_pane, state, data, notice);
            feed.Sync();
            TgApp.AddTimeout(TimeSpan.FromMilliseconds(60), () => { Pump(capture); return true; });
            TgApp.AddTimeout(TimeSpan.FromSeconds(1), () => { _panels.Refresh(); return true; });
            _panels.Refresh();
            _layout.Input.SetFocus();
            _afterInput = () => { Pump(capture); feed.Sync(); _panels.Refresh(); };
            TgApp.Run(_layout.Root);
        }
        finally
        {
            feed?.Dispose();
            _layout?.Root.Dispose();
            capture.Dispose();
            TgApp.Shutdown();
        }

        await driver.ShutdownAsync(state).ConfigureAwait(false);
        Render.Info(state.Role == SessionRole.Driver ? "Session saved. Goodbye." : "Detached from the observed session. Goodbye.");
        return 0;
    }

    private void Build(string mode)
    {
        _layout = new TuiLayout(TuiKeys.Shortcuts(this));
        _pane = new TranscriptPane(_layout.Transcript);
        _presenter = new TuiPresenter(_pane, approvals);
        _panels = new TuiPanels(_layout, state, status, spans, options, data, logger) { Mode = mode };
        _layout.Input.Accepting += (_, e) =>
        {
            e.Handled = true;
            string text = _layout.Input.Text;
            _layout.Input.Text = string.Empty;
            Submit(text);
        };
        _layout.Root.KeyDown += (_, key) => TuiKeys.Handle(this, key);
        _panels.UpdateTitles();
    }

    /// <summary>Sends a line (prompt or command) as if typed.</summary>
    public void Submit(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        _history.Add(text);
        _pane.AppendLine((text.StartsWith('/') ? "› " : "❯ ") + text);
        _ = Task.Run(async () =>
        {
            try
            {
                await driver.HandleAsync(state, _presenter, text).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                logger.LogError(ex, "Input handling failed");
                _presenter.Error(ex.Message);
            }

            TgApp.Invoke(() =>
            {
                _afterInput();
                if (state.ExitRequested)
                {
                    TgApp.RequestStop(_layout.Root);
                }
            });
        });
    }

    /// <summary>Esc: cancels the run in progress.</summary>
    public void CancelRun()
    {
        if (SessionDriver.CancelRun(state))
        {
            _presenter.Warn("Cancelling the current run…");
        }
    }

    /// <summary>Ctrl+Q: cancels any run and leaves.</summary>
    public void Quit()
    {
        SessionDriver.CancelRun(state);
        state.ExitRequested = true;
        TgApp.RequestStop(_layout.Root);
    }

    /// <summary>F2: toggles plan/execute.</summary>
    public void ToggleMode() => Submit(_panels.Mode == AgentModes.Execute ? "/mode plan" : "/mode execute");

    /// <summary>F5: observer takes over, driver detaches.</summary>
    public void TakeoverOrDetach() => Submit(state.Role == SessionRole.Observer ? "/takeover" : "/detach");

    /// <summary>Up/Down in the prompt: walks the input history.</summary>
    public void History(int delta)
    {
        if (_layout.Input.HasFocus && _history.Move(delta) is { } text)
        {
            _layout.Input.Text = text;
        }
    }

    private void Pump(SpectreCapture capture)
    {
        _presenter.FlushPending();
        string captured = capture.Drain();
        if (captured.Length > 0)
        {
            _pane.Append((_pane.AtLineStart ? string.Empty : "\n") + captured.TrimEnd() + "\n");
        }

        _panels.UpdateTitles();
    }
}
