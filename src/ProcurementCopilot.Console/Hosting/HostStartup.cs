using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ProcurementCopilot.ConsoleApp.Hosting;

/// <summary>Start-up steps that the generic host would run for us; done explicitly so the console owns Ctrl+C.</summary>
public static class HostStartup
{
    /// <summary>Runs every <c>ValidateOnStart</c> options validation. Throws <see cref="OptionsValidationException"/> naming the section, never a value.</summary>
    public static void ValidateOptions(IServiceProvider services) =>
        services.GetService<IStartupValidator>()?.Validate();

    /// <summary>Materialises the OpenTelemetry providers so exporters start.</summary>
    public static void StartTelemetry(IServiceProvider services)
    {
        _ = services.GetService<TracerProvider>();
        _ = services.GetService<MeterProvider>();
    }
}
