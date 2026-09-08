#pragma warning disable CS0618 // Terminal.Gui 2.4.17 flags TextView and the static Application facade obsolete ahead of v3; both are the stable, documented API of this release.
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.Infrastructure.SqlServer;
using TgApp = Terminal.Gui.App.Application;

namespace ProcurementCopilot.ConsoleApp.Tui;

/// <summary>Starts and stops the observer poller as the role changes and writes the driver's activity into the transcript.</summary>
public sealed class TuiObserverFeed : IDisposable
{
    private readonly AppState _state;
    private readonly ObserverPoller _poller;
    private readonly TuiPresenter _presenter;
    private readonly TuiPanels _panels;
    private bool _observing;

    /// <summary>Initializes the feed.</summary>
    public TuiObserverFeed(AppState state, ObserverPoller poller, TuiPresenter presenter, TuiPanels panels)
    {
        _state = state;
        _poller = poller;
        _presenter = presenter;
        _panels = panels;
        _poller.Changed += OnObserved;
    }

    /// <summary>Starts polling when this instance observes, stops when it drives.</summary>
    public void Sync()
    {
        bool shouldObserve = _state.Role == SessionRole.Observer;
        if (shouldObserve && !_observing)
        {
            _poller.Start(_state, TimeSpan.FromSeconds(1));
            _presenter.Info("Following the driver's status file: tool calls and activity changes appear here as they happen; panels refresh every second.");
        }
        else if (!shouldObserve && _observing)
        {
            _poller.Stop();
        }

        _observing = shouldObserve;
    }

    /// <summary>Writes the welcome text into the transcript.</summary>
    public static void Welcome(TranscriptPane pane, AppState state, DataProviderSelection data, string? notice)
    {
        Guid id = SessionIdentity.GetOrCreate(state.Session);
        pane.AppendLine($"Procurement Copilot · {(data.Provider == "SqlServer" ? "Adventure Works Cycles" : "Contoso Industrial Systems")} · harness demo on Microsoft Agent Framework");
        pane.AppendLine($"Data: {data.Description}");
        pane.AppendLine($"Session {id:N}. Watch it from a second terminal: --attach {id:N}");
        pane.AppendLine(state.FakeMode ? "FAKE MODE: canned run, no model calls. After the plan: /mode execute, then \"go\"." : "Suggested first prompt: Evaluate RFP-2026-017 and recommend a vendor.");
        pane.AppendLine("Keys: Enter send · Esc cancel run · F1 help · F2 mode · F3 todos · F4 traces · F5 take over/detach · F6 sessions · F7 who drives · Ctrl+Q quit · Up/Down prompt history · Tab focuses the panels for scrolling");
        if (data.Warning is not null)
        {
            pane.AppendLine("! " + data.Warning);
        }

        if (notice is not null)
        {
            pane.AppendLine("! " + notice);
        }
    }

    private void OnObserved(ObserverUpdate update)
    {
        if (update.ActivityChanged)
        {
            string tool = update.Status.CurrentTool is null ? string.Empty : $" ({update.Status.CurrentTool})";
            _presenter.Info($"[driver {update.Status.Instance}] {update.Status.Activity}{tool}, mode {update.Status.Mode}");
        }

        foreach (ToolEvent tool in update.NewTools)
        {
            _presenter.ToolCall(tool.Name, tool.Arguments, tool.Result);
        }

        TgApp.Invoke(_panels.Refresh);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _poller.Changed -= OnObserved;
        _poller.Stop();
    }
}
