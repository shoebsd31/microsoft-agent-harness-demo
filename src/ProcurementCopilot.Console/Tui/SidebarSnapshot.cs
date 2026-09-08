using System.Globalization;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.ConsoleApp.Ui;
using ProcurementCopilot.Infrastructure.Telemetry;

namespace ProcurementCopilot.ConsoleApp.Tui;

/// <summary>Text for the four side panels, computed off the UI thread from the live state (driver) or the status file (observer).</summary>
/// <param name="Mode">Current agent mode.</param>
/// <param name="Todos">Todos panel text.</param>
/// <param name="Tasks">Background tasks panel text.</param>
/// <param name="Context">Context panel text.</param>
/// <param name="Traces">Traces panel text.</param>
public sealed record SidebarSnapshot(string Mode, string Todos, string Tasks, string Context, string Traces)
{
    /// <summary>Builds a snapshot. <paramref name="live"/> is this instance's own status when driving, or the observed status.</summary>
    public static async Task<SidebarSnapshot> BuildAsync(AppState state, SessionStatus live, SpanRingBuffer spans, ProcurementAgentOptions options)
    {
        bool observer = state.Role == SessionRole.Observer;
        string mode = observer ? live.Mode : await state.GetModeAsync().ConfigureAwait(false);
        IReadOnlyList<TodoItem> todos = state.Todos is { } provider ? await provider.GetAllTodosAsync(state.Session).ConfigureAwait(false) : [];
        return new SidebarSnapshot(mode, TodosText(todos), TasksText(state, live, observer), ContextText(state, live, observer, mode, options), TracesText(live, spans, observer));
    }

    private static string TodosText(IReadOnlyList<TodoItem> todos)
    {
        if (todos.Count == 0)
        {
            return "No todos yet. The agent writes its plan here.";
        }

        var sb = new StringBuilder();
        foreach (TodoItem item in todos)
        {
            sb.Append(item.IsComplete ? "✓ " : "○ ").Append(item.Id.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(item.Title).Append('\n');
        }

        return sb.ToString().TrimEnd();
    }

    private static string TasksText(AppState state, SessionStatus live, bool observer)
    {
        IReadOnlyCollection<TrackedTask> tasks = observer ? live.Tasks : state.Tasks.Tasks.Values;
        if (tasks.Count == 0)
        {
            return "No background tasks yet (market-research, risk-analyst).";
        }

        IReadOnlyList<BackgroundTaskInfo> running = state.Agent.GetService<BackgroundAgentsProvider>()?.GetIncompleteTasks(state.Session) ?? [];
        var sb = new StringBuilder();
        foreach (TrackedTask task in tasks)
        {
            string status = running.Any(r => r.Id == task.Id) ? "running" : task.Status;
            sb.Append('#').Append(task.Id).Append(' ').Append(task.Agent).Append(" · ").Append(status).Append('\n').Append("   ").Append(Render.Truncate(task.Result ?? task.Description, 120)).Append('\n');
        }

        return sb.ToString().TrimEnd();
    }

    private static string ContextText(AppState state, SessionStatus live, bool observer, string mode, ProcurementAgentOptions options)
    {
        int budget = options.Agent.MaxContextWindowTokens - options.Agent.MaxOutputTokens;
        List<ChatMessage> history = state.Factory.HistoryProvider?.GetMessages(state.Session) ?? [];
        UsageSnapshot usage = observer ? live.Usage : new UsageSnapshot(state.Usage.LastInputTokens, state.Usage.LastOutputTokens, state.Usage.TotalTokens, state.Usage.Reports);
        int compactions = observer ? live.CompactionCount : state.Factory.CompactionStrategy?.CompactionCount ?? 0;
        var sb = new StringBuilder();
        sb.Append("Role      ").Append(observer ? $"observer of {live.Instance}" : "driver (" + SessionLock.InstanceName + ")").Append('\n');
        sb.Append("Mode      ").Append(mode).Append("   Activity ").Append(live.Activity).Append(live.CurrentTool is null ? string.Empty : " (" + live.CurrentTool + ")").Append('\n');
        sb.Append("Data      ").Append(live.DataProvider).Append('\n');
        sb.Append("Last call ").Append(usage.LastInputTokens is { } i ? $"in {i:N0} ({100.0 * i / budget:F1}% of {budget:N0}), out {usage.LastOutputTokens:N0}" : "no usage yet").Append('\n');
        sb.Append("Session   ").Append($"{usage.TotalTokens:N0} tokens / {usage.Reports} calls, {history.Count} stored messages").Append('\n');
        sb.Append("Compaction").Append(' ').Append(compactions > 0 ? $"fired {compactions}×" : "armed, not fired").Append('\n');
        sb.Append("Approvals ").Append(state.StandingApprovals.Count == 0 ? "none standing" : string.Join(", ", state.StandingApprovals));
        if (live.Outstanding.Count > 0)
        {
            sb.Append('\n').Append("Outstanding ").Append(string.Join("; ", live.Outstanding));
        }

        return sb.ToString();
    }

    private static string TracesText(SessionStatus live, SpanRingBuffer spans, bool observer)
    {
        IEnumerable<SpanEvent> recent = observer
            ? live.RecentSpans.Take(10)
            : spans.Recent(10).Select(s => new SpanEvent(s.Name, s.Source, s.Duration.TotalMilliseconds, s.Status));
        var sb = new StringBuilder();
        foreach (SpanEvent span in recent)
        {
            sb.Append(Render.Truncate(span.Name, 34).PadRight(34)).Append(' ').Append($"{span.DurationMilliseconds,7:N0} ms ").Append(span.Status).Append('\n');
        }

        return sb.Length == 0 ? (observer ? "Spans arrive when the driver finishes a turn." : "No spans yet; /traces shows the full list.") : sb.ToString().TrimEnd();
    }
}
