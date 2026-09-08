#pragma warning disable CS0618 // Terminal.Gui 2.4.17 flags TextView and the static Application facade obsolete ahead of v3; both are the stable, documented API of this release.
using Microsoft.Extensions.Logging;
using ProcurementCopilot.Agent;
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.Infrastructure.SqlServer;
using ProcurementCopilot.Infrastructure.Telemetry;
using TgApp = Terminal.Gui.App.Application;

namespace ProcurementCopilot.ConsoleApp.Tui;

/// <summary>Keeps the side panels and the frame titles current. Refreshes are computed off the UI thread and applied on it.</summary>
public sealed class TuiPanels
{
    private readonly TuiLayout _layout;
    private readonly AppState _state;
    private readonly StatusPublisher _status;
    private readonly SpanRingBuffer _spans;
    private readonly ProcurementAgentOptions _options;
    private readonly DataProviderSelection _data;
    private readonly ILogger _logger;
    private bool _refreshing;

    /// <summary>Initializes the panel updater.</summary>
    public TuiPanels(TuiLayout layout, AppState state, StatusPublisher status, SpanRingBuffer spans, ProcurementAgentOptions options, DataProviderSelection data, ILogger logger)
    {
        _layout = layout;
        _state = state;
        _status = status;
        _spans = spans;
        _options = options;
        _data = data;
        _logger = logger;
    }

    /// <summary>Mode as of the last refresh.</summary>
    public string Mode { get; set; } = AgentModes.Plan;

    /// <summary>The status that describes what is happening: this instance's own when driving, the driver's when observing.</summary>
    public SessionStatus Live => _state.Role == SessionRole.Observer ? _state.ObservedStatus ?? new SessionStatus() : _status.Status;

    /// <summary>UI thread: updates the window and prompt titles (mode, role, activity, current tool).</summary>
    public void UpdateTitles()
    {
        SessionStatus live = Live;
        string activity = live.Activity + (live.CurrentTool is null ? string.Empty : " " + live.CurrentTool);
        _layout.InputFrame.Title = $" {Mode.ToUpperInvariant()} · {(_state.Role == SessionRole.Observer ? "OBSERVER (read-only)" : "driver")} · {activity} ";
        _layout.Root.Title = $" Procurement Copilot · {SessionIdentity.GetOrCreate(_state.Session):N} · {_data.Provider} ";
    }

    /// <summary>UI thread: schedules a refresh of the four panels unless one is already running.</summary>
    public void Refresh()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        SessionStatus live = Live;
        _ = Task.Run(async () =>
        {
            try
            {
                SidebarSnapshot snapshot = await SidebarSnapshot.BuildAsync(_state, live, _spans, _options).ConfigureAwait(false);
                TgApp.Invoke(() => Apply(snapshot));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger.LogDebug(ex, "Sidebar refresh failed");
                TgApp.Invoke(() => _refreshing = false);
            }
        });
    }

    private void Apply(SidebarSnapshot snapshot)
    {
        _refreshing = false;
        Mode = snapshot.Mode;
        TuiLayout.SetText(_layout.Todos, snapshot.Todos);
        TuiLayout.SetText(_layout.Tasks, snapshot.Tasks);
        TuiLayout.SetText(_layout.Context, snapshot.Context);
        TuiLayout.SetText(_layout.Traces, snapshot.Traces);
        UpdateTitles();
    }
}
