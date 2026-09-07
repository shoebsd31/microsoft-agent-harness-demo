namespace ProcurementCopilot.Application.Configuration;

/// <summary>OpenTelemetry settings bound from the <c>OpenTelemetry</c> section.</summary>
public sealed class TelemetryOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OpenTelemetry";

    /// <summary>OTLP collector endpoint. Empty selects the local file exporter.</summary>
    public string OtlpEndpoint { get; set; } = string.Empty;

    /// <summary>Export prompt and completion content. Off by default.</summary>
    public bool EnableSensitiveData { get; set; }

    /// <summary>Also write spans to the console (noisy; off by default).</summary>
    public bool ConsoleExporter { get; set; }

    /// <summary>Gets a value indicating whether an OTLP endpoint is configured.</summary>
    public bool UseOtlp => !string.IsNullOrWhiteSpace(OtlpEndpoint);
}
