using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.ConsoleApp.Ui;

namespace ProcurementCopilot.ConsoleApp.Tui;

/// <summary>
/// Presenter for the TUI. Agent output arrives on thread-pool threads, so everything is queued as plain text and
/// flushed into the <see cref="TranscriptPane"/> by a UI-thread timer (see <see cref="FlushPending"/>).
/// </summary>
public sealed class TuiPresenter : ITurnPresenter
{
    private readonly TranscriptPane _pane;
    private readonly ApprovalPrompt _approvals;
    private readonly StringBuilder _pending = new();
    private readonly object _gate = new();
    private bool _atLineStart = true;

    /// <summary>Initializes the presenter.</summary>
    public TuiPresenter(TranscriptPane pane, ApprovalPrompt approvals)
    {
        _pane = pane;
        _approvals = approvals;
    }

    /// <summary>Queues raw text.</summary>
    public void Enqueue(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            _pending.Append(text);
            _atLineStart = text[^1] == '\n';
        }
    }

    /// <summary>Queues a full line (starting a new line first when needed).</summary>
    public void EnqueueLine(string line)
    {
        lock (_gate)
        {
            bool atStart = _pending.Length == 0 ? _pane.AtLineStart : _atLineStart;
            _pending.Append(atStart ? string.Empty : "\n").Append(line).Append('\n');
            _atLineStart = true;
        }
    }

    /// <summary>UI thread: moves queued text into the pane.</summary>
    public void FlushPending()
    {
        string text;
        lock (_gate)
        {
            if (_pending.Length == 0)
            {
                return;
            }

            text = _pending.ToString();
            _pending.Clear();
        }

        _pane.Append(text);
    }

    /// <inheritdoc />
    public void Text(string fragment) => Enqueue(fragment);

    /// <inheritdoc />
    public void EndText()
    {
        lock (_gate)
        {
            bool atStart = _pending.Length == 0 ? _pane.AtLineStart : _atLineStart;
            if (!atStart)
            {
                _pending.Append('\n');
                _atLineStart = true;
            }
        }
    }

    /// <inheritdoc />
    public void ToolCall(string name, string arguments, string result) => EnqueueLine($"  ▸ {name}({arguments}) → {result}");

    /// <inheritdoc />
    public void ToolStarted(string name)
    {
        // The prompt frame title shows the running tool; the transcript line follows when the result arrives.
    }

    /// <inheritdoc />
    public void LoopFeedback(string text) => EnqueueLine("  ↻ loop: " + Render.Truncate(text, 200));

    /// <inheritdoc />
    public void Error(string message) => EnqueueLine("✖ " + message);

    /// <inheritdoc />
    public void Cancelled() => EnqueueLine("! Run cancelled.");

    /// <inheritdoc />
    public void Info(string message) => EnqueueLine("· " + message);

    /// <inheritdoc />
    public void Warn(string message) => EnqueueLine("! " + message);

    /// <inheritdoc />
    public void TurnCompleted(IReadOnlyList<TodoItem> todos, IReadOnlyList<string> outstanding)
    {
        EnqueueLine(todos.Count == 0 ? "  ✓ turn complete" : $"  ✓ turn complete · todos {todos.Count(t => t.IsComplete)}/{todos.Count} (see the Todos panel)");
        if (outstanding.Count > 0)
        {
            EnqueueLine("! Loop budget exhausted or run interrupted. Still outstanding: " + string.Join("; ", outstanding));
        }
    }

    /// <inheritdoc />
    public async Task<AIContent> ApproveAsync(ToolApprovalRequestContent request, AppState state)
    {
        string name = ApprovalPrompt.ToolName(request);
        EnqueueLine($"  🔐 approval required for {name}");
        string choice = await ApprovalDialog.AskAsync(FlushPending, name, ApprovalPrompt.Arguments(request)).ConfigureAwait(false);
        AIContent response = await _approvals.ResolveAsync(request, state, choice).ConfigureAwait(false);
        EnqueueLine(choice == "n" ? $"  ✖ denied {name}" : $"  ✔ {ApprovalPrompt.Describe(choice)} {name}");
        return response;
    }
}
