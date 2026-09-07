using Microsoft.Extensions.Configuration;
using ProcurementCopilot.Application.Security;
using Serilog;
using Serilog.Events;

namespace ProcurementCopilot.Infrastructure.Logging;

/// <summary>Builds the Serilog pipeline: rolling files under <c>logs/</c>, everything redacted. Nothing is written to the console.</summary>
public static class SerilogSetup
{
    /// <summary>Configures the logger. The console stays free for the agent UX.</summary>
    public static LoggerConfiguration Configure(LoggerConfiguration configuration, IConfiguration appConfiguration, SecretRedactor redactor, string logsDirectory)
    {
        string level = appConfiguration["Serilog:MinimumLevel"] ?? "Information";
        var fileLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(Path.Combine(logsDirectory, "procurement-copilot-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true)
            .CreateLogger();

        return configuration
            .MinimumLevel.Is(Enum.TryParse(level, ignoreCase: true, out LogEventLevel parsed) ? parsed : LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Sink(new RedactingSink(fileLogger, redactor));
    }
}
