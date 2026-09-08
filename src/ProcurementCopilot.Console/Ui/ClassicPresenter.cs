using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ProcurementCopilot.ConsoleApp.Runtime;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Ui;

/// <summary>Line-oriented presenter (Spectre.Console) used when stdin is redirected or <c>--classic</c> is passed.</summary>
public sealed class ClassicPresenter : ITurnPresenter
{
    private readonly ApprovalPrompt _approvals;
    private bool _textOpen;

    /// <summary>Initializes the presenter.</summary>
    public ClassicPresenter(ApprovalPrompt approvals) => _approvals = approvals;

    /// <inheritdoc />
    public void Text(string fragment)
    {
        AnsiConsole.Write(fragment);
        _textOpen = true;
    }

    /// <inheritdoc />
    public void EndText()
    {
        if (_textOpen)
        {
            AnsiConsole.WriteLine();
            _textOpen = false;
        }
    }

    /// <inheritdoc />
    public void ToolCall(string name, string arguments, string result) => Render.ToolCall(name, arguments, result);

    /// <inheritdoc />
    public void ToolStarted(string name)
    {
        // The classic UI prints the collapsed line when the result arrives.
    }

    /// <inheritdoc />
    public void LoopFeedback(string text) => Render.LoopFeedback(text);

    /// <inheritdoc />
    public void Error(string message) => Render.Error(message);

    /// <inheritdoc />
    public void Cancelled() => Render.Warn("Run cancelled.");

    /// <inheritdoc />
    public void Info(string message) => Render.Info(message);

    /// <inheritdoc />
    public void Warn(string message) => Render.Warn(message);

    /// <inheritdoc />
    public void TurnCompleted(IReadOnlyList<TodoItem> todos, IReadOnlyList<string> outstanding)
    {
        Render.Todos(todos);
        if (outstanding.Count > 0)
        {
            Render.Warn("Loop budget exhausted or run interrupted. Still outstanding: " + string.Join("; ", outstanding));
        }
    }

    /// <inheritdoc />
    public Task<AIContent> ApproveAsync(ToolApprovalRequestContent request, AppState state) => _approvals.AskAsync(request, state);
}
