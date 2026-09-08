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
using ProcurementCopilot.Infrastructure.SqlServer;
using ProcurementCopilot.Infrastructure.Telemetry;
using ProcurementCopilot.Infrastructure.Time;

namespace ProcurementCopilot.Infrastructure.DependencyInjection;

/// <summary>Registers repositories (JSON or SQL Server), stores, the model client factory and telemetry.</summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>Adds infrastructure services. <paramref name="logsDirectory"/> receives trace files when no OTLP endpoint is set.</summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, string? logsDirectory = null)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ISessionStore>(sp => new FileSessionStore(sp.GetRequiredService<IOptions<SessionsOptions>>().Value.ResolveDirectory(), sp.GetRequiredService<IClock>()));
        services.AddSingleton<IOutbox, FileOutbox>();
        services.AddSingleton<IAuditLog, JsonlAuditLog>();
        services.AddSingleton<IChatClientFactory, FoundryChatClientFactory>();
        services.AddSingleton<FileAwardRecorder>();
        services.AddDataBackend(configuration);

        var telemetry = new TelemetryOptions();
        configuration.GetSection(TelemetryOptions.SectionName).Bind(telemetry);
        var redactor = new SecretRedactor(configuration[$"{FoundryOptions.SectionName}:{nameof(FoundryOptions.ApiKey)}"]);
        services.AddProcurementTelemetry(telemetry, redactor, logsDirectory ?? Path.Combine(AppContext.BaseDirectory, "logs"));
        return services;
    }

    /// <summary>Chooses JSON or SQL Server from <c>Data:Provider</c> and registers the matching repositories.</summary>
    public static IServiceCollection AddDataBackend(this IServiceCollection services, IConfiguration configuration)
    {
        var data = new DataOptions();
        configuration.GetSection(DataOptions.SectionName).Bind(data);
        var sql = new SqlServerOptions();
        configuration.GetSection(SqlServerOptions.SectionName).Bind(sql);
        string jsonDirectory = DataFileLocator.Locate();

        DataProviderSelection selection = DataProviderSelector.Resolve(data, sql, jsonDirectory);
        services.AddSingleton(selection);
        services.AddSingleton<IDataProviderInfo>(selection);
        services.AddSingleton(new SqlConnectionFactory(sql));
        services.AddSingleton<SqlMigrationRunner>();
        services.AddSingleton<IProcurementAdminStore, ProcurementAdminStore>();

        if (selection.IsSqlServer)
        {
            services.AddSingleton<IRfpRepository, SqlRfpRepository>();
            services.AddSingleton<IVendorRepository, SqlVendorRepository>();
            services.AddSingleton<IBidRepository, SqlBidRepository>();
            services.AddSingleton<ISanctionsRepository, SqlRestrictedPartyRepository>();
            services.AddSingleton<IFxRateRepository, SqlCurrencyRateRepository>();
            services.AddSingleton<IAwardRecorder>(sp => new SqlAwardRecorder(sp.GetRequiredService<SqlConnectionFactory>(), sp.GetRequiredService<FileAwardRecorder>()));
            services.AddSingleton<IReadOnlyQueryExecutor, SqlReadOnlyQueryExecutor>();
            return services;
        }

        services.AddSingleton(_ => new SeedDataStore(jsonDirectory));
        services.AddSingleton<IRfpRepository, JsonRfpRepository>();
        services.AddSingleton<IVendorRepository, JsonVendorRepository>();
        services.AddSingleton<IBidRepository, JsonBidRepository>();
        services.AddSingleton<ISanctionsRepository, CsvSanctionsRepository>();
        services.AddSingleton<IFxRateRepository, JsonFxRateRepository>();
        services.AddSingleton<IAwardRecorder>(sp => sp.GetRequiredService<FileAwardRecorder>());
        services.AddSingleton<IReadOnlyQueryExecutor, UnavailableQueryExecutor>();
        return services;
    }
}
