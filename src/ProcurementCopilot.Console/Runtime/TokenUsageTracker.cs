using Microsoft.Extensions.AI;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>Accumulates token usage reported by the model for the <c>/context</c> command.</summary>
public sealed class TokenUsageTracker
{
    /// <summary>Input tokens of the most recent model call.</summary>
    public long? LastInputTokens { get; private set; }

    /// <summary>Output tokens of the most recent model call.</summary>
    public long? LastOutputTokens { get; private set; }

    /// <summary>Total tokens across the session.</summary>
    public long TotalTokens { get; private set; }

    /// <summary>Number of usage reports received.</summary>
    public int Reports { get; private set; }

    /// <summary>Records a usage report.</summary>
    public void Record(UsageDetails? usage)
    {
        if (usage is null)
        {
            return;
        }

        Reports++;
        LastInputTokens = usage.InputTokenCount ?? LastInputTokens;
        LastOutputTokens = usage.OutputTokenCount ?? LastOutputTokens;
        TotalTokens += usage.TotalTokenCount ?? (usage.InputTokenCount ?? 0) + (usage.OutputTokenCount ?? 0);
    }
}

/// <summary>Tracks background tasks by watching the <c>background_agents_*</c> tool calls and results.</summary>
public sealed class BackgroundTaskTracker
{
    private readonly Dictionary<string, string> _pendingStarts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _pendingResults = new(StringComparer.Ordinal);

    /// <summary>Known tasks by id.</summary>
    public SortedDictionary<int, TrackedTask> Tasks { get; } = [];

    /// <summary>Records a tool call; start-task calls are remembered until their result reveals the task id.</summary>
    public void OnCall(FunctionCallContent call)
    {
        if (call.Name == "background_agents_start_task" && call.Arguments is not null)
        {
            string agent = call.Arguments.TryGetValue("agentName", out object? a) ? a?.ToString() ?? "?" : "?";
            string description = call.Arguments.TryGetValue("description", out object? d) ? d?.ToString() ?? string.Empty : string.Empty;
            _pendingStarts[call.CallId] = agent + "|" + description;
        }
        else if (call.Name == "background_agents_get_task_results" && call.Arguments is not null && call.Arguments.TryGetValue("taskId", out object? t) && int.TryParse(t?.ToString(), out int taskId))
        {
            _pendingResults[call.CallId] = taskId;
        }
    }

    /// <summary>Records a tool result.</summary>
    public void OnResult(FunctionResultContent result)
    {
        string text = result.Result?.ToString() ?? string.Empty;
        if (_pendingStarts.Remove(result.CallId, out string? pending))
        {
            string[] parts = pending.Split('|', 2);
            int id = ExtractInt(text);
            Tasks[id] = new TrackedTask(id, parts[0], parts.Length > 1 ? parts[1] : string.Empty, "running", null);
            return;
        }

        if (_pendingResults.Remove(result.CallId, out int resultTask) && Tasks.TryGetValue(resultTask, out TrackedTask? tracked))
        {
            bool stillRunning = text.StartsWith("Task", StringComparison.Ordinal) && text.Contains("running", StringComparison.OrdinalIgnoreCase);
            Tasks[resultTask] = tracked with { Status = stillRunning ? "running" : text.Contains("failed", StringComparison.OrdinalIgnoreCase) && text.Length < 200 ? "failed" : "completed", Result = stillRunning ? tracked.Result : Trim(text) };
            return;
        }

        foreach (KeyValuePair<int, TrackedTask> entry in Tasks.ToList())
        {
            if (text.Contains($"task {entry.Key}", StringComparison.OrdinalIgnoreCase) || text.Contains($"Task {entry.Key}", StringComparison.Ordinal) || text.Contains($"\"{entry.Key}\"", StringComparison.Ordinal))
            {
                string status = text.Contains("failed", StringComparison.OrdinalIgnoreCase) ? "failed" : text.Contains("running", StringComparison.OrdinalIgnoreCase) ? "running" : "completed";
                Tasks[entry.Key] = entry.Value with { Status = status, Result = status == "completed" ? Trim(text) : entry.Value.Result };
            }
        }
    }

    private static int ExtractInt(string text)
    {
        string digits = new(text.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out int id) ? id : 0;
    }

    private static string Trim(string text) => text.Length <= 160 ? text : text[..160] + "…";
}

/// <summary>A background task as seen by the console.</summary>
/// <param name="Id">Task id.</param>
/// <param name="Agent">Child agent name.</param>
/// <param name="Description">Task description.</param>
/// <param name="Status">running, completed or failed.</param>
/// <param name="Result">Result excerpt when completed.</param>
public sealed record TrackedTask(int Id, string Agent, string Description, string Status, string? Result);
