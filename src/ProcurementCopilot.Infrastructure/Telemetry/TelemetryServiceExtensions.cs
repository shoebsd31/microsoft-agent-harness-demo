using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Application.Security;

namespace ProcurementCopilot.Infrastructure.Telemetry;

/// <summary>Registers OpenTelemetry tracing and metrics for the harness and the application.</summary>
public static class TelemetryServiceExtensions
{
    /// <summary>Activity source used for the harness agent and for custom application spans.</summary>
    public const string ApplicationSourceName = "ProcurementCopilot.Agent";

    /// <summary>Default Agent Framework source name (also subscribed so nothing is lost).</summary>
    public const string AgentFrameworkSourceName = "Experimental.Microsoft.Agents.AI";

    /// <summary>Environment variable the Microsoft.Extensions.AI instrumentation reads to capture message content.</summary>
    public const string SensitiveDataEnvironmentVariable = "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT";

    /// <summary>Adds tracing + metrics with the OTLP exporter when configured, otherwise the file exporter and the in-memory buffer.</summary>
    public static IServiceCollection AddProcurementTelemetry(this IServiceCollection services, TelemetryOptions options, SecretRedactor redactor, string logsDirectory)
    {
        if (options.EnableSensitiveData)
        {
            Environment.SetEnvironmentVariable(SensitiveDataEnvironmentVariable, "true");
        }

        var buffer = new SpanRingBuffer();
        services.AddSingleton(buffer);
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("ProcurementCopilot"))
            .WithTracing(tracing =>
            {
                tracing.AddSource(ApplicationSourceName).AddSource(AgentFrameworkSourceName).AddSource("Microsoft.Extensions.AI");
                tracing.AddProcessor(new RedactingProcessor(redactor));
                tracing.AddProcessor(new SpanRingBufferProcessor(buffer));
                if (options.UseOtlp)
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(options.OtlpEndpoint));
                }
                else
                {
                    tracing.AddProcessor(new SimpleActivityExportProcessor(new FileSpanExporter(logsDirectory)));
                }

                if (options.ConsoleExporter)
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(ApplicationSourceName).AddMeter(AgentFrameworkSourceName).AddMeter("Microsoft.Extensions.AI");
                if (options.UseOtlp)
                {
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(options.OtlpEndpoint));
                }
                else if (options.ConsoleExporter)
                {
                    metrics.AddConsoleExporter();
                }
            });
        return services;
    }
}
