using System.Text.Json.Serialization;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>A tool call as shown in the live status.</summary>
public sealed record ToolEvent(DateTimeOffset At, string Name, string Arguments, string Result);

/// <summary>Token usage snapshot.</summary>
public sealed record UsageSnapshot(long? LastInputTokens, long? LastOutputTokens, long TotalTokens, int Reports);

/// <summary>A finished span as shown in the live status.</summary>
public sealed record SpanEvent(string Name, string Source, double DurationMilliseconds, string Status);

/// <summary>
/// Live status of a session, written by the instance that drives it to <c>{sessionId}.status.json</c> next to the
/// session file, so another console instance can attach and watch (mode, activity, tools, tasks, usage, spans).
/// </summary>
public sealed class SessionStatus
{
    /// <summary>Session id.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Human-readable instance name (machine and process id).</summary>
    public string Instance { get; set; } = string.Empty;

    /// <summary>Process id of the driving instance.</summary>
    public int Pid { get; set; }

    /// <summary>When the status was last written.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Data backend in use.</summary>
    public string DataProvider { get; set; } = string.Empty;

    /// <summary>Current agent mode.</summary>
    public string Mode { get; set; } = "plan";

    /// <summary><c>idle</c>, <c>running</c>, <c>awaiting-approval</c> or <c>exited</c>.</summary>
    public string Activity { get; set; } = "idle";

    /// <summary>Tool currently executing or awaiting approval.</summary>
    public string? CurrentTool { get; set; }

    /// <summary>Last prompt the analyst typed.</summary>
    public string? LastPrompt { get; set; }

    /// <summary>Most recent tool calls, newest last.</summary>
    public List<ToolEvent> RecentTools { get; set; } = [];

    /// <summary>Token usage.</summary>
    public UsageSnapshot Usage { get; set; } = new(null, null, 0, 0);

    /// <summary>Background tasks as tracked by the console.</summary>
    public List<TrackedTask> Tasks { get; set; } = [];

    /// <summary>How many times compaction fired.</summary>
    public int CompactionCount { get; set; }

    /// <summary>Most recent spans, newest first.</summary>
    public List<SpanEvent> RecentSpans { get; set; } = [];

    /// <summary>Outstanding evaluation items reported by the loop evaluator.</summary>
    public List<string> Outstanding { get; set; } = [];
}

/// <summary>Who currently drives a session (<c>{sessionId}.lock</c>).</summary>
public sealed class SessionLockInfo
{
    /// <summary>Session id.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Instance name.</summary>
    public string Instance { get; set; } = string.Empty;

    /// <summary>Process id.</summary>
    public int Pid { get; set; }

    /// <summary>When the lock was taken.</summary>
    public DateTimeOffset AcquiredAt { get; set; }

    /// <summary>Last heartbeat.</summary>
    public DateTimeOffset HeartbeatAt { get; set; }
}

/// <summary>JSON context for the status and lock files.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SessionStatus))]
[JsonSerializable(typeof(SessionLockInfo))]
public sealed partial class StatusJsonContext : JsonSerializerContext;
