using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.ConsoleApp.Ui;
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
        SessionStatus? observed = state.Role == SessionRole.Observer ? state.ObservedStatus : null;
        UsageSnapshot usage = observed?.Usage ?? new UsageSnapshot(state.Usage.LastInputTokens, state.Usage.LastOutputTokens, state.Usage.TotalTokens, state.Usage.Reports);
        table.AddRow("Last model call", usage.LastInputTokens is { } i ? $"input {i:N0} ({100.0 * i / budget:F1}% of budget), output {usage.LastOutputTokens:N0}" : "no usage reported yet");
        table.AddRow("Session total", $"{usage.TotalTokens:N0} tokens over {usage.Reports} reports");
        int fired = observed?.CompactionCount ?? state.Factory.CompactionStrategy?.CompactionCount ?? 0;
        table.AddRow("Compaction", fired > 0 ? $"[yellow]fired {fired}×[/] (ProcurementCompactionStrategy)" : "not fired (ProcurementCompactionStrategy armed)");
        if (observed is not null)
        {
            table.AddRow("Source", Markup.Escape($"status file of driver {observed.Instance}, updated {observed.UpdatedAt.ToLocalTime():HH:mm:ss}"));
        }

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
