using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ProcurementCopilot.Application.Configuration;

/// <summary>Binds and validates every options section at start-up.</summary>
public static class OptionsRegistration
{
    /// <summary>Registers all strongly typed options with data-annotation validation.</summary>
    public static IServiceCollection AddProcurementOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FoundryOptions>().Bind(configuration.GetSection(FoundryOptions.SectionName)).ValidateDataAnnotations()
            .Validate(o => o.HasValidEndpoint, $"{FoundryOptions.SectionName}:{nameof(FoundryOptions.Endpoint)} must be an absolute http(s) URL when set.").ValidateOnStart();
        services.AddOptions<AgentOptions>().Bind(configuration.GetSection(AgentOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<WorkspaceOptions>().Bind(configuration.GetSection(WorkspaceOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SessionsOptions>().Bind(configuration.GetSection(SessionsOptions.SectionName)).ValidateOnStart();
        services.AddOptions<TelemetryOptions>().Bind(configuration.GetSection(TelemetryOptions.SectionName)).ValidateOnStart();
        services.AddOptions<DataOptions>().Bind(configuration.GetSection(DataOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SqlServerOptions>().Bind(configuration.GetSection(SqlServerOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        return services;
    }
}
