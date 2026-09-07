using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Application.Abstractions;

namespace ProcurementCopilot.Testing;

/// <summary>One request the fake received.</summary>
/// <param name="Messages">The messages sent to the model.</param>
/// <param name="Options">The chat options.</param>
/// <param name="Streaming">Whether the streaming API was used.</param>
public sealed record ScriptedRequest(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options, bool Streaming);

/// <summary>
/// A fake <see cref="IChatClient"/> that replays a scripted sequence of <see cref="ChatResponse"/>s (text and function
/// calls), supports streaming, and records every request it receives. Shared by the tests and the console fake mode.
/// </summary>
public sealed class ScriptedChatClient : IChatClient, ISupportsHostedWebSearch
{
    private readonly Queue<ChatResponse> _script = new();
    private readonly List<ScriptedRequest> _requests = [];
    private int _counter;

    /// <summary>Initializes an empty script.</summary>
    public ScriptedChatClient(bool supportsWebSearch = false) => SupportsWebSearch = supportsWebSearch;

    /// <summary>Initializes the client with a script.</summary>
    public ScriptedChatClient(IEnumerable<ChatResponse> script, bool supportsWebSearch = false) : this(supportsWebSearch) => Enqueue(script);

    /// <summary>Gets a value indicating whether this fake advertises hosted web search support.</summary>
    public bool SupportsWebSearch { get; }

    /// <inheritdoc />
    public bool HostedWebSearchSupported => SupportsWebSearch;

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyList<ScriptedRequest> Requests => _requests;

    /// <summary>Number of model calls made.</summary>
    public int CallCount => _requests.Count;

    /// <summary>Number of scripted responses not yet consumed.</summary>
    public int Remaining => _script.Count;

    /// <summary>Called when the script is exhausted. Defaults to a short text reply.</summary>
    public Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> Fallback { get; set; } =
        (_, _) => Script.Text("(scripted client: no more responses in the script)");

    /// <summary>Optional delay per streamed chunk, for a realistic console demo.</summary>
    public TimeSpan StreamDelay { get; set; } = TimeSpan.Zero;

    /// <summary>Adds responses to the end of the script.</summary>
    public ScriptedChatClient Enqueue(IEnumerable<ChatResponse> responses)
    {
        foreach (ChatResponse response in responses)
        {
            _script.Enqueue(response);
        }

        return this;
    }

    /// <summary>Adds one response to the end of the script.</summary>
    public ScriptedChatClient Enqueue(ChatResponse response)
    {
        _script.Enqueue(response);
        return this;
    }

    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Next(messages.ToList(), options, streaming: false));

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = Next(messages.ToList(), options, streaming: true);
        foreach (ChatResponseUpdate update in Script.ToStreamingUpdates(response))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (StreamDelay > TimeSpan.Zero)
            {
                await Task.Delay(StreamDelay, cancellationToken).ConfigureAwait(false);
            }

            yield return update;
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>Synthesises token usage (chars / 4) so usage-driven UX can be demonstrated offline.</summary>
    public static UsageDetails EstimateUsage(IReadOnlyList<ChatMessage> request, ChatResponse response)
    {
        long input = request.Sum(m => (m.Text?.Length ?? 0) + m.Contents.OfType<FunctionResultContent>().Sum(r => r.Result?.ToString()?.Length ?? 0)) / 4;
        long output = response.Messages.Sum(m => m.Text?.Length ?? 0) / 4 + 8;
        return new UsageDetails { InputTokenCount = input, OutputTokenCount = output, TotalTokenCount = input + output };
    }

    private ChatResponse Next(List<ChatMessage> messages, ChatOptions? options, bool streaming)
    {
        _requests.Add(new ScriptedRequest(messages, options, streaming));
        ChatResponse response = _script.Count > 0 ? _script.Dequeue() : Fallback(messages, options);
        int n = Interlocked.Increment(ref _counter);
        response.ResponseId ??= $"resp-{n:000}";
        response.Usage ??= EstimateUsage(messages, response);
        foreach (ChatMessage message in response.Messages)
        {
            message.MessageId ??= $"msg-{n:000}";
        }

        return response;
    }
}
