namespace ProcurementCopilot.Application.Configuration;

/// <summary>Model provider settings (Azure AI Foundry / Azure OpenAI with an API key). Secrets come from user-secrets or environment variables.</summary>
public sealed class FoundryOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Foundry";

    /// <summary>Foundry project or Azure OpenAI endpoint URL. Validated as an absolute http(s) URL when present.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>API key. Never logged; see <c>SecretRedactor</c>.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Chat model deployment name.</summary>
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>Optional judge deployment; defaults to <see cref="DeploymentName"/>.</summary>
    public string JudgeDeploymentName { get; set; } = string.Empty;

    /// <summary>Append <c>/openai/v1</c> to the endpoint when it is missing (Foundry project endpoints).</summary>
    public bool UseV1Path { get; set; } = true;

    /// <summary>Store responses server-side. Off by default so history and compaction stay in-process.</summary>
    public bool StoreResponses { get; set; }

    /// <summary>Gets the effective judge deployment.</summary>
    public string EffectiveJudgeDeployment =>
        string.IsNullOrWhiteSpace(JudgeDeploymentName) ? DeploymentName : JudgeDeploymentName;

    /// <summary>Returns <see langword="true"/> when the endpoint is empty or an absolute http(s) URL.</summary>
    public bool HasValidEndpoint =>
        string.IsNullOrWhiteSpace(Endpoint) ||
        (Uri.TryCreate(Endpoint, UriKind.Absolute, out Uri? uri) && uri.Scheme is "https" or "http");

    /// <summary>Gets a value indicating whether every field needed for a live model call is present.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(DeploymentName);

    /// <summary>Names the missing configuration keys (never their values).</summary>
    public IReadOnlyList<string> MissingKeys()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            missing.Add($"{SectionName}:{nameof(Endpoint)}");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            missing.Add($"{SectionName}:{nameof(ApiKey)}");
        }

        if (string.IsNullOrWhiteSpace(DeploymentName))
        {
            missing.Add($"{SectionName}:{nameof(DeploymentName)}");
        }

        return missing;
    }
}
