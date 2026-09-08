using Microsoft.Agents.AI;
using ProcurementCopilot.Agent;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>Mutable state shared by the console loop, the turn runner and the commands.</summary>
public sealed class AppState
{
    /// <summary>The composed harness agent.</summary>
    public HarnessAgent Agent { get; set; } = null!;

    /// <summary>The active session.</summary>
    public AgentSession Session { get; set; } = null!;

    /// <summary>The factory that built the agent (exposes the evaluator, compaction strategy and history provider).</summary>
    public HarnessAgentFactory Factory { get; set; } = null!;

    /// <summary>Whether the app runs on the scripted client.</summary>
    public bool FakeMode { get; set; }

    /// <summary>Cancellation for the run in progress, or <see langword="null"/> when idle.</summary>
    public CancellationTokenSource? RunCancellation { get; set; }

    /// <summary>Set when the user asked to leave.</summary>
    public bool ExitRequested { get; set; }

    /// <summary>Token usage from the last model response.</summary>
    public TokenUsageTracker Usage { get; } = new();

    /// <summary>Tools the user chose to always approve in this session.</summary>
    public List<string> StandingApprovals { get; } = [];

    /// <summary>Background tasks observed in tool calls.</summary>
    public BackgroundTaskTracker Tasks { get; } = new();

    /// <summary>Whether this instance drives the session or only observes it.</summary>
    public SessionRole Role { get; set; } = SessionRole.Driver;

    /// <summary>Latest status file read while observing (null when driving).</summary>
    public SessionStatus? ObservedStatus { get; set; }

    /// <summary>True while an agent turn is in flight.</summary>
    public bool IsRunning => RunCancellation is not null;

    /// <summary>Gets the mode provider of the agent.</summary>
    public AgentModeProvider? Modes => Agent.GetService<AgentModeProvider>();

    /// <summary>Gets the todo provider of the agent.</summary>
    public TodoProvider? Todos => Agent.GetService<TodoProvider>();

    /// <summary>Reads the current mode.</summary>
    public async Task<string> GetModeAsync() => Modes is null ? "?" : await Modes.GetModeAsync(Session).ConfigureAwait(false);
}
