using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Responses;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Configuration;

namespace ProcurementCopilot.Infrastructure.Chat;

/// <summary>
/// Builds <see cref="IChatClient"/>s for Azure AI Foundry / Azure OpenAI using an API key and the Responses API,
/// which is required for the hosted web-search tool. Never logs or echoes the key.
/// </summary>
public sealed class FoundryChatClientFactory : IChatClientFactory
{
    private readonly FoundryOptions _options;

    /// <summary>Initializes the factory from validated options.</summary>
    public FoundryChatClientFactory(IOptions<FoundryOptions> options) => _options = options.Value;

    /// <inheritdoc />
    public IChatClient CreateChatClient() => Create(_options.DeploymentName);

    /// <inheritdoc />
    public IChatClient CreateJudgeClient() => Create(_options.EffectiveJudgeDeployment);

    /// <summary>Computes the effective endpoint, appending <c>/openai/v1</c> when configured and missing.</summary>
    public static Uri BuildEndpoint(FoundryOptions options)
    {
        var uri = new Uri(options.Endpoint, UriKind.Absolute);
        if (!options.UseV1Path || uri.AbsolutePath.Contains("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        var builder = new UriBuilder(uri) { Path = uri.AbsolutePath.TrimEnd('/') + "/openai/v1/" };
        return builder.Uri;
    }

    private IChatClient Create(string deployment)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("Foundry is not configured. Missing: " + string.Join(", ", _options.MissingKeys()) + ". Set them with 'dotnet user-secrets set' or environment variables.");
        }

        var clientOptions = new OpenAIClientOptions { Endpoint = BuildEndpoint(_options) };
        clientOptions.AddPolicy(new ApiKeyHeaderPolicy(_options.ApiKey), PipelinePosition.PerCall);
        var openAi = new OpenAIClient(new ApiKeyCredential(_options.ApiKey), clientOptions);
        ResponsesClient responses = openAi.GetResponsesClient();
        return _options.StoreResponses
            ? responses.AsIChatClient(deployment)
            : responses.AsIChatClientWithStoredOutputDisabled(deployment);
    }
}
