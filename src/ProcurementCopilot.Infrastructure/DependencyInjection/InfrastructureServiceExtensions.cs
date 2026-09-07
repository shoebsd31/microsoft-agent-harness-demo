using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Infrastructure.Audit;
using ProcurementCopilot.Infrastructure.Chat;
using ProcurementCopilot.Infrastructure.Data;
using ProcurementCopilot.Infrastructure.Outbox;
using ProcurementCopilot.Infrastructure.Repositories;
using ProcurementCopilot.Infrastructure.Sessions;
using ProcurementCopilot.Infrastructure.Telemetry;
using ProcurementCopilot.Infrastructure.Time;

namespace ProcurementCopilot.Infrastructure.DependencyInjection;

/// <summary>Registers repositories, stores, the model client factory and telemetry.</summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>Adds infrastructure services. <paramref name="logsDirectory"/> receives trace files when no OTLP endpoint is set.</summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, string? logsDirectory = null)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(_ => new SeedDataStore(DataFileLocator.Locate()));
        services.AddSingleton<IRfpRepository, JsonRfpRepository>();
        services.AddSingleton<IVendorRepository, JsonVendorRepository>();
        services.AddSingleton<IBidRepository, JsonBidRepository>();
        services.AddSingleton<ISanctionsRepository, CsvSanctionsRepository>();
        services.AddSingleton<IFxRateRepository, JsonFxRateRepository>();

        services.AddSingleton<ISessionStore>(sp => new FileSessionStore(sp.GetRequiredService<IOptions<SessionsOptions>>().Value.ResolveDirectory(), sp.GetRequiredService<IClock>()));
        services.AddSingleton<IOutbox, FileOutbox>();
        services.AddSingleton<IAuditLog, JsonlAuditLog>();
        services.AddSingleton<IChatClientFactory, FoundryChatClientFactory>();

        var telemetry = new TelemetryOptions();
        configuration.GetSection(TelemetryOptions.SectionName).Bind(telemetry);
        var redactor = new SecretRedactor(configuration[$"{FoundryOptions.SectionName}:{nameof(FoundryOptions.ApiKey)}"]);
        services.AddProcurementTelemetry(telemetry, redactor, logsDirectory ?? Path.Combine(AppContext.BaseDirectory, "logs"));
        return services;
    }
}
