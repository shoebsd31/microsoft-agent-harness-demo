using Microsoft.Extensions.AI;

namespace ProcurementCopilot.Application.Abstractions;

/// <summary>Creates model clients for the main agent and the optional judge.</summary>
public interface IChatClientFactory
{
    /// <summary>Creates the chat client for the main deployment.</summary>
    IChatClient CreateChatClient();

    /// <summary>Creates the chat client for the judge deployment.</summary>
    IChatClient CreateJudgeClient();
}

/// <summary>Chat clients that can declare whether the provider-hosted web-search tool is available.</summary>
public interface ISupportsHostedWebSearch
{
    /// <summary>Gets a value indicating whether hosted web search can be used with this client.</summary>
    bool HostedWebSearchSupported { get; }
}
