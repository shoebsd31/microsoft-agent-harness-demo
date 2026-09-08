using System.Globalization;
using Microsoft.Agents.AI;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.ConsoleApp.Ui;
using ProcurementCopilot.Infrastructure.Telemetry;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Commands;

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
        IReadOnlyCollection<TrackedTask> tasks = state.Role == SessionRole.Observer && state.ObservedStatus is { } observed ? observed.Tasks : state.Tasks.Tasks.Values;
        var table = new Table().Border(TableBorder.Simple).AddColumn("Id").AddColumn("Agent").AddColumn("Status").AddColumn("Task / result");
        foreach (TrackedTask task in tasks)
        {
            bool stillRunning = running.Any(r => r.Id == task.Id);
            string status = stillRunning ? "[yellow]running[/]" : task.Status == "failed" ? "[red]failed[/]" : "[green]" + task.Status + "[/]";
            table.AddRow(task.Id.ToString(CultureInfo.InvariantCulture), Markup.Escape(task.Agent), status, Markup.Escape(task.Result ?? task.Description));
        }

        if (tasks.Count == 0)
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
        if (state.Role == SessionRole.Observer && state.ObservedStatus is { } observed)
        {
            foreach (SpanEvent span in observed.RecentSpans.Take(15))
            {
                table.AddRow(Markup.Escape(Render.Truncate(span.Name, 50)), Markup.Escape(span.Source), $"{span.DurationMilliseconds:N0} ms", Markup.Escape(span.Status));
            }

            AnsiConsole.Write(table);
            Render.Info($"Spans published by driver {observed.Instance} at the end of its last turn; full traces are in that instance's logs/traces-*.jsonl.");
            return Task.CompletedTask;
        }

        foreach (SpanSummary span in spans.Recent(15))
        {
            table.AddRow(Markup.Escape(Render.Truncate(span.Name, 50)), Markup.Escape(span.Source), $"{span.Duration.TotalMilliseconds:N0} ms", Markup.Escape(span.Status));
        }

        AnsiConsole.Write(table);
        Render.Info($"{spans.TotalRecorded} spans recorded this session.");
        return Task.CompletedTask;
    }
}
