using System.ClientModel.Primitives;

namespace ProcurementCopilot.Infrastructure.Chat;

/// <summary>Adds the Azure <c>api-key</c> header to every request so both bearer and header authentication work against the v1 endpoint.</summary>
public sealed class ApiKeyHeaderPolicy : PipelinePolicy
{
    private readonly string _apiKey;

    /// <summary>Initializes the policy with the key to send.</summary>
    public ApiKeyHeaderPolicy(string apiKey) => _apiKey = apiKey;

    /// <inheritdoc />
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Set("api-key", _apiKey);
        ProcessNext(message, pipeline, currentIndex);
    }

    /// <inheritdoc />
    public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Set("api-key", _apiKey);
        return ProcessNextAsync(message, pipeline, currentIndex);
    }
}
