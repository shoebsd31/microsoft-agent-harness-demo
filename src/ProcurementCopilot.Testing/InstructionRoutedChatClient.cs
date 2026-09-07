using Microsoft.Extensions.AI;

namespace ProcurementCopilot.Testing;

/// <summary>
/// Routes each request to a named <see cref="IChatClient"/> by looking for a route key in the instructions or system
/// messages. Lets the fake background agents (which share one client) each replay their own script deterministically.
/// </summary>
public sealed class InstructionRoutedChatClient : IChatClient
{
    private readonly IReadOnlyDictionary<string, IChatClient> _routes;
    private readonly IChatClient _fallback;

    /// <summary>Initializes the router.</summary>
    public InstructionRoutedChatClient(IReadOnlyDictionary<string, IChatClient> routes, IChatClient fallback)
    {
        _routes = routes;
        _fallback = fallback;
    }

    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> list = messages.ToList();
        return Route(list, options).GetResponseAsync(list, options, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> list = messages.ToList();
        return Route(list, options).GetStreamingResponseAsync(list, options, cancellationToken);
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : _fallback.GetService(serviceType, serviceKey);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private IChatClient Route(List<ChatMessage> messages, ChatOptions? options)
    {
        string haystack = (options?.Instructions ?? string.Empty) + "\n" + string.Join("\n", messages.Where(m => m.Role == ChatRole.System).Select(m => m.Text));
        foreach ((string key, IChatClient client) in _routes)
        {
            if (haystack.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return client;
            }
        }

        return _fallback;
    }
}
