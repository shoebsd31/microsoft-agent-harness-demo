using Microsoft.Extensions.AI;

namespace ProcurementCopilot.Testing;

/// <summary>Builders for scripted model responses.</summary>
public static class Script
{
    private static int _callCounter;

    /// <summary>A plain assistant text response.</summary>
    public static ChatResponse Text(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text)) { FinishReason = ChatFinishReason.Stop };

    /// <summary>An assistant response containing a single function call.</summary>
    public static ChatResponse Call(string name, params IEnumerable<KeyValuePair<string, object?>> arguments) =>
        Calls([CallContent(name, arguments)]);

    /// <summary>An assistant response with text followed by a function call.</summary>
    public static ChatResponse TextThenCall(string text, string name, params IEnumerable<KeyValuePair<string, object?>> arguments) =>
        new(new ChatMessage(ChatRole.Assistant, [new TextContent(text), CallContent(name, arguments)])) { FinishReason = ChatFinishReason.ToolCalls };

    /// <summary>An assistant response containing several function calls (executed sequentially by the harness).</summary>
    public static ChatResponse Calls(params IEnumerable<FunctionCallContent> calls) =>
        new(new ChatMessage(ChatRole.Assistant, calls.Cast<AIContent>().ToList())) { FinishReason = ChatFinishReason.ToolCalls };

    /// <summary>Creates a function-call content with a unique call id.</summary>
    public static FunctionCallContent CallContent(string name, IEnumerable<KeyValuePair<string, object?>> arguments) =>
        new($"call-{Interlocked.Increment(ref _callCounter):0000}", name, arguments.ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal));

    /// <summary>Shorthand for a named argument.</summary>
    public static KeyValuePair<string, object?> Arg(string name, object? value) => new(name, value);

    /// <summary>Splits a response into streaming updates: text is chunked by words, other contents pass through whole.</summary>
    public static IEnumerable<ChatResponseUpdate> ToStreamingUpdates(ChatResponse response)
    {
        foreach (ChatMessage message in response.Messages)
        {
            foreach (AIContent content in message.Contents)
            {
                if (content is TextContent text && text.Text.Length > 0)
                {
                    foreach (string chunk in Chunk(text.Text))
                    {
                        yield return Update(response, message, new TextContent(chunk));
                    }
                }
                else
                {
                    yield return Update(response, message, content);
                }
            }
        }

        yield return new ChatResponseUpdate(ChatRole.Assistant, Array.Empty<AIContent>())
        {
            ResponseId = response.ResponseId,
            MessageId = response.Messages.LastOrDefault()?.MessageId,
            FinishReason = response.FinishReason,
            Contents = response.Usage is null ? [] : [new UsageContent(response.Usage)],
        };
    }

    private static ChatResponseUpdate Update(ChatResponse response, ChatMessage message, AIContent content) =>
        new(message.Role, [content]) { ResponseId = response.ResponseId, MessageId = message.MessageId, ModelId = response.ModelId };

    private static IEnumerable<string> Chunk(string text)
    {
        int start = 0;
        while (start < text.Length)
        {
            int next = text.IndexOf(' ', start);
            int end = next < 0 ? text.Length : next + 1;
            yield return text[start..end];
            start = end;
        }
    }
}
