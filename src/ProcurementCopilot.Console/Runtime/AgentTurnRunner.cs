using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ProcurementCopilot.ConsoleApp.Ui;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>Streams one agent turn: renders text tokens, collapsed tool calls, loop feedback and approval prompts.</summary>
public sealed class AgentTurnRunner
{
    private readonly ApprovalPrompt _approvals;
    private readonly ILogger<AgentTurnRunner> _logger;

    /// <summary>Initializes the runner.</summary>
    public AgentTurnRunner(ApprovalPrompt approvals, ILogger<AgentTurnRunner> logger)
    {
        _approvals = approvals;
        _logger = logger;
    }

    /// <summary>Runs the agent on the user's input, handling approvals until the turn completes or is cancelled.</summary>
    public async Task RunTurnAsync(AppState state, string userInput, CancellationToken cancellationToken)
    {
        IList<ChatMessage> next = [new ChatMessage(ChatRole.User, userInput)];
        while (next.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            List<ToolApprovalRequestContent> requests = await StreamAsync(state, next, cancellationToken).ConfigureAwait(false);
            next = [];
            foreach (ToolApprovalRequestContent request in requests)
            {
                AIContent response = await _approvals.AskAsync(request, state).ConfigureAwait(false);
                next.Add(new ChatMessage(ChatRole.User, [response]));
            }
        }
    }

    private async Task<List<ToolApprovalRequestContent>> StreamAsync(AppState state, IList<ChatMessage> messages, CancellationToken cancellationToken)
    {
        var pendingCalls = new Dictionary<string, FunctionCallContent>(StringComparer.Ordinal);
        var requests = new List<ToolApprovalRequestContent>();
        bool textOpen = false;
        try
        {
            await foreach (AgentResponseUpdate update in state.Agent.RunStreamingAsync(messages, state.Session, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                foreach (AIContent content in update.Contents)
                {
                    textOpen = Handle(state, update, content, pendingCalls, requests, textOpen);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Render.Warn("\nRun cancelled.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Agent turn failed");
            Render.Error("The agent run failed: " + ex.GetType().Name + ". Details are in the log file under logs/.");
        }

        if (textOpen)
        {
            AnsiConsole.WriteLine();
        }

        return requests;
    }

    private static bool Handle(AppState state, AgentResponseUpdate update, AIContent content, Dictionary<string, FunctionCallContent> pending, List<ToolApprovalRequestContent> requests, bool textOpen)
    {
        switch (content)
        {
            case TextContent text when update.Role == ChatRole.User:
                CloseText(ref textOpen);
                Render.LoopFeedback(text.Text);
                return false;
            case TextContent text when !string.IsNullOrEmpty(text.Text):
                AnsiConsole.Write(text.Text);
                return true;
            case FunctionCallContent call:
                CloseText(ref textOpen);
                pending[call.CallId] = call;
                state.Tasks.OnCall(call);
                return false;
            case FunctionResultContent result:
                CloseText(ref textOpen);
                state.Tasks.OnResult(result);
                if (pending.Remove(result.CallId, out FunctionCallContent? completed))
                {
                    Render.ToolCall(completed.Name, ToolResultSummarizer.Arguments(completed), ToolResultSummarizer.Result(result.Result));
                }

                return false;
            case ToolApprovalRequestContent request:
                CloseText(ref textOpen);
                requests.Add(request);
                return false;
            case UsageContent usage:
                state.Usage.Record(usage.Details);
                return textOpen;
            case ErrorContent error:
                CloseText(ref textOpen);
                Render.Error(error.Message ?? "unknown error");
                return false;
            default:
                return textOpen;
        }
    }

    private static void CloseText(ref bool textOpen)
    {
        if (textOpen)
        {
            AnsiConsole.WriteLine();
            textOpen = false;
        }
    }
}
