using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ProcurementCopilot.ConsoleApp.Ui;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>Streams one agent turn to an <see cref="ITurnPresenter"/>, handles approvals, and publishes live status.</summary>
public sealed class AgentTurnRunner
{
    private readonly StatusPublisher _status;
    private readonly ILogger<AgentTurnRunner> _logger;

    /// <summary>Initializes the runner.</summary>
    public AgentTurnRunner(StatusPublisher status, ILogger<AgentTurnRunner> logger)
    {
        _status = status;
        _logger = logger;
    }

    /// <summary>Runs the agent on the user's input, handling approvals until the turn completes or is cancelled.</summary>
    public async Task RunTurnAsync(AppState state, ITurnPresenter presenter, string userInput, CancellationToken cancellationToken)
    {
        _status.Update(s => { s.Activity = "running"; s.LastPrompt = Render.Truncate(userInput, 200); s.CurrentTool = null; });
        IList<ChatMessage> next = [new ChatMessage(ChatRole.User, userInput)];
        try
        {
            while (next.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                List<ToolApprovalRequestContent> requests = await StreamAsync(state, presenter, next, cancellationToken).ConfigureAwait(false);
                next = [];
                foreach (ToolApprovalRequestContent request in requests)
                {
                    string tool = (request.ToolCall as FunctionCallContent)?.Name ?? "tool";
                    _status.Update(s => { s.Activity = "awaiting-approval"; s.CurrentTool = tool; });
                    AIContent response = await presenter.ApproveAsync(request, state).ConfigureAwait(false);
                    _status.Update(s => { s.Activity = "running"; s.CurrentTool = null; });
                    next.Add(new ChatMessage(ChatRole.User, [response]));
                }
            }
        }
        finally
        {
            _status.Update(s => { s.Activity = "idle"; s.CurrentTool = null; s.Usage = new UsageSnapshot(state.Usage.LastInputTokens, state.Usage.LastOutputTokens, state.Usage.TotalTokens, state.Usage.Reports); });
        }
    }

    private async Task<List<ToolApprovalRequestContent>> StreamAsync(AppState state, ITurnPresenter presenter, IList<ChatMessage> messages, CancellationToken cancellationToken)
    {
        var pending = new Dictionary<string, FunctionCallContent>(StringComparer.Ordinal);
        var requests = new List<ToolApprovalRequestContent>();
        try
        {
            await foreach (AgentResponseUpdate update in state.Agent.RunStreamingAsync(messages, state.Session, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                foreach (AIContent content in update.Contents)
                {
                    Handle(state, presenter, update, content, pending, requests);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            presenter.EndText();
            presenter.Cancelled();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Agent turn failed");
            presenter.EndText();
            presenter.Error("The agent run failed: " + ex.GetType().Name + ". Details are in the log file under logs/.");
        }

        presenter.EndText();
        return requests;
    }

    private void Handle(AppState state, ITurnPresenter presenter, AgentResponseUpdate update, AIContent content, Dictionary<string, FunctionCallContent> pending, List<ToolApprovalRequestContent> requests)
    {
        switch (content)
        {
            case TextContent text when update.Role == ChatRole.User:
                presenter.EndText();
                presenter.LoopFeedback(text.Text);
                break;
            case TextContent text when !string.IsNullOrEmpty(text.Text):
                presenter.Text(text.Text);
                break;
            case FunctionCallContent call:
                presenter.EndText();
                pending[call.CallId] = call;
                state.Tasks.OnCall(call);
                presenter.ToolStarted(call.Name);
                _status.Update(s => s.CurrentTool = call.Name);
                break;
            case FunctionResultContent result:
                presenter.EndText();
                state.Tasks.OnResult(result);
                if (pending.Remove(result.CallId, out FunctionCallContent? completed))
                {
                    string arguments = ToolResultSummarizer.Arguments(completed);
                    string summary = ToolResultSummarizer.Result(result.Result);
                    presenter.ToolCall(completed.Name, arguments, summary);
                    _status.Update(s => { s.CurrentTool = null; s.RecentTools.Add(new ToolEvent(DateTimeOffset.UtcNow, completed.Name, arguments, summary)); s.Tasks = state.Tasks.Tasks.Values.ToList(); });
                }

                break;
            case ToolApprovalRequestContent request:
                presenter.EndText();
                requests.Add(request);
                break;
            case UsageContent usage:
                state.Usage.Record(usage.Details);
                break;
            case ErrorContent error:
                presenter.EndText();
                presenter.Error(error.Message ?? "unknown error");
                break;
            default:
                break;
        }
    }
}
