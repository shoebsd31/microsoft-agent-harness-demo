using System.Runtime.CompilerServices;

namespace ProcurementCopilot.Integration.Tests;

/// <summary>A fact that is skipped (not failed) unless the Foundry endpoint, key and deployment are present in the environment.</summary>
public sealed class FoundryFactAttribute : FactAttribute
{
    public FoundryFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!Live.IsConfigured)
        {
            Skip = "Set Foundry__Endpoint, Foundry__ApiKey and Foundry__DeploymentName to run live integration tests.";
        }
    }
}

/// <summary>Reads the live settings from environment variables only.</summary>
public static class Live
{
    public static string? Endpoint => Environment.GetEnvironmentVariable("Foundry__Endpoint");

    public static string? ApiKey => Environment.GetEnvironmentVariable("Foundry__ApiKey");

    public static string? Deployment => Environment.GetEnvironmentVariable("Foundry__DeploymentName");

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Deployment);
}
