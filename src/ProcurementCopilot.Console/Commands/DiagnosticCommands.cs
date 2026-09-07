using System.Globalization;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.ConsoleApp.Ui;
using ProcurementCopilot.Infrastructure.Telemetry;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Commands;

/// <summary><c>/context</c>: estimated token usage and whether compaction has fired.</summary>
public sealed class ContextCommand(ProcurementAgentOptions options) : IConsoleCommand
{
    /// <inheritdoc />
    public string Name => "/context";

    /// <inheritdoc />
    public string Help => "show token usage, context budget and compaction status";

    /// <inheritdoc />
    public Task ExecuteAsync(string[] args, AppState state)
    {
        int budget = options.Agent.MaxContextWindowTokens - options.Agent.MaxOutputTokens;
        List<ChatMessage> history = state.Factory.HistoryProvider?.GetMessages(state.Session) ?? [];
        long estimated = history.Sum(m => (m.Text?.Length ?? 0) + m.Contents.OfType<FunctionResultContent>().Sum(r => r.Result?.ToString()?.Length ?? 0)) / 4;
        var table = new Table().Border(TableBorder.Simple).AddColumn("Metric").AddColumn("Value");
        table.AddRow("Context window / output reserve", $"{options.Agent.MaxContextWindowTokens:N0} / {options.Agent.MaxOutputTokens:N0} tokens (input budget {budget:N0})");
        table.AddRow("Stored history", $"{history.Count} messages, ~{estimated:N0} tokens (chars/4 estimate)");
        table.AddRow("Last model call", state.Usage.LastInputTokens is { } i ? $"input {i:N0} ({100.0 * i / budget:F1}% of budget), output {state.Usage.LastOutputTokens:N0}" : "no usage reported yet");
        table.AddRow("Session total", $"{state.Usage.TotalTokens:N0} tokens over {state.Usage.Reports} reports");
        int fired = state.Factory.CompactionStrategy?.CompactionCount ?? 0;
        table.AddRow("Compaction", fired > 0 ? $"[yellow]fired {fired}×[/], {state.Factory.CompactionStrategy!.LastIncludedTokenCount:N0} tokens kept (ProcurementCompactionStrategy)" : "not fired (ProcurementCompactionStrategy armed)");
        AnsiConsole.Write(table);
        return Task.CompletedTask;
    }
}

/// <summary><c>/approvals</c>: standing approvals; <c>/approvals clear</c> revokes them.</summary>
public sealed class ApprovalsCommand : IConsoleCommand
{
    /// <inheritdoc />
    public string Name => "/approvals";

    /// <inheritdoc />
    public string Help => "/approvals [clear] - list or revoke standing approvals for this session";

    /// <inheritdoc />
    public Task ExecuteAsync(string[] args, AppState state)
    {
        if (args.FirstOrDefault()?.Equals("clear", StringComparison.OrdinalIgnoreCase) == true)
        {
            int removed = RemoveApprovalState(state.Session);
            state.StandingApprovals.Clear();
            Render.Info($"Standing approvals cleared ({removed} state entries removed). Tools will prompt again.");
            return Task.CompletedTask;
        }

        Render.Info(state.StandingApprovals.Count == 0 ? "No standing approvals." : "Always-approved this session: " + string.Join(", ", state.StandingApprovals));
        return Task.CompletedTask;
    }

    private static int RemoveApprovalState(AgentSession session)
    {
        JsonElement bag = session.StateBag.Serialize();
        List<string> keys = bag.ValueKind == JsonValueKind.Object
            ? bag.EnumerateObject().Select(p => p.Name).Where(k => k.Contains("Approval", StringComparison.OrdinalIgnoreCase)).ToList()
            : [];
        return keys.Count(session.StateBag.TryRemoveValue);
    }
}

/// <summary><c>/tasks</c>: background agent tasks and their status.</summary>
public sealed class TasksCommand : IConsoleCommand
{
    /// <inheritdoc />
    public string Name => "/tasks";

    /// <inheritdoc />
    public string Help => "show background agent tasks (market-research, risk-analyst)";

    /// <inheritdoc />
    public Task ExecuteAsync(string[] args, AppState state)
    {
        BackgroundAgentsProvider? provider = state.Agent.GetService<BackgroundAgentsProvider>();
        IReadOnlyList<BackgroundTaskInfo> running = provider?.GetIncompleteTasks(state.Session) ?? [];
        var table = new Table().Border(TableBorder.Simple).AddColumn("Id").AddColumn("Agent").AddColumn("Status").AddColumn("Task / result");
        foreach (TrackedTask task in state.Tasks.Tasks.Values)
        {
            bool stillRunning = running.Any(r => r.Id == task.Id);
            string status = stillRunning ? "[yellow]running[/]" : task.Status == "failed" ? "[red]failed[/]" : "[green]" + task.Status + "[/]";
            table.AddRow(task.Id.ToString(CultureInfo.InvariantCulture), Markup.Escape(task.Agent), status, Markup.Escape(task.Result ?? task.Description));
        }

        if (state.Tasks.Tasks.Count == 0)
        {
            Render.Info("No background tasks yet.");
        }
        else
        {
            AnsiConsole.Write(table);
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>/traces</c>: recent OpenTelemetry spans from the in-memory buffer.</summary>
public sealed class TracesCommand(SpanRingBuffer spans) : IConsoleCommand
{
    /// <inheritdoc />
    public string Name => "/traces";

    /// <inheritdoc />
    public string Help => "show the most recent OpenTelemetry spans (full traces are in logs/traces-*.jsonl or OTLP)";

    /// <inheritdoc />
    public Task ExecuteAsync(string[] args, AppState state)
    {
        var table = new Table().Border(TableBorder.Simple).AddColumn("Span").AddColumn("Source").AddColumn("Duration").AddColumn("Status");
        foreach (SpanSummary span in spans.Recent(15))
        {
            table.AddRow(Markup.Escape(Render.Truncate(span.Name, 50)), Markup.Escape(span.Source), $"{span.Duration.TotalMilliseconds:N0} ms", Markup.Escape(span.Status));
        }

        AnsiConsole.Write(table);
        Render.Info($"{spans.TotalRecorded} spans recorded this session.");
        return Task.CompletedTask;
    }
}
