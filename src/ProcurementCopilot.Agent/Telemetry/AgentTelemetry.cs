using System.Diagnostics;

namespace ProcurementCopilot.Agent.Telemetry;

/// <summary>The custom activity source for procurement spans; the harness also emits under this name.</summary>
public static class AgentTelemetry
{
    /// <summary>Source name registered with the tracer provider.</summary>
    public const string SourceName = "ProcurementCopilot.Agent";

    /// <summary>The shared activity source.</summary>
    public static readonly ActivitySource Source = new(SourceName);
}
