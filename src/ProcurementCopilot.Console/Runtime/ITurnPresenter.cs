using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>
/// Everything a front-end must render during an agent turn. The classic line UI and the TUI both implement it, so the
/// streaming logic in <see cref="AgentTurnRunner"/> is written once.
/// </summary>
public interface ITurnPresenter
{
    /// <summary>Streamed assistant text (may be a fragment).</summary>
    void Text(string fragment);

    /// <summary>Ends the current text block, if one is open.</summary>
    void EndText();

    /// <summary>A completed tool call.</summary>
    void ToolCall(string name, string arguments, string result);

    /// <summary>A tool call that started (approval granted or auto-approved) and is executing.</summary>
    void ToolStarted(string name);

    /// <summary>Loop evaluator feedback injected on the analyst's behalf.</summary>
    void LoopFeedback(string text);

    /// <summary>A recoverable error.</summary>
    void Error(string message);

    /// <summary>The run was cancelled by the analyst.</summary>
    void Cancelled();

    /// <summary>An informational line (grey in the classic UI).</summary>
    void Info(string message);

    /// <summary>A warning line.</summary>
    void Warn(string message);

    /// <summary>Called once per completed turn with the todo list and any outstanding loop-evaluator items.</summary>
    void TurnCompleted(IReadOnlyList<TodoItem> todos, IReadOnlyList<string> outstanding);

    /// <summary>Asks the analyst to approve a tool call and returns the response content for the agent.</summary>
    Task<AIContent> ApproveAsync(ToolApprovalRequestContent request, AppState state);
}
