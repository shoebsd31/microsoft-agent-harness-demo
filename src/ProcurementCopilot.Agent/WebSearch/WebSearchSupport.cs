using Microsoft.Extensions.AI;
using OpenAI.Responses;
using ProcurementCopilot.Application.Abstractions;

namespace ProcurementCopilot.Agent.WebSearch;

/// <summary>Detects whether a chat client can execute the provider-hosted web-search tool.</summary>
public static class WebSearchSupport
{
    /// <summary>Returns <see langword="true"/> for OpenAI Responses clients and for clients that implement <see cref="ISupportsHostedWebSearch"/>.</summary>
    public static bool IsSupported(IChatClient chatClient) =>
        chatClient is ISupportsHostedWebSearch declared ? declared.HostedWebSearchSupported : chatClient.GetService<ResponsesClient>() is not null;
}
