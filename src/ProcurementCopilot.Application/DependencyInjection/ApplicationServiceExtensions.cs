using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Application.Services;

namespace ProcurementCopilot.Application.DependencyInjection;

/// <summary>Registers application use cases and security policies.</summary>
public static class ApplicationServiceExtensions
{
    /// <summary>Adds options, policies and use-case services. Repositories and stores come from Infrastructure.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProcurementOptions(configuration);

        services.AddSingleton(sp =>
        {
            WorkspaceOptions workspace = sp.GetRequiredService<IOptions<WorkspaceOptions>>().Value;
            return new WorkspacePathPolicy(workspace.ResolveRoot(AppContext.BaseDirectory), workspace);
        });
        services.AddSingleton(sp => new ShellCommandPolicy(sp.GetRequiredService<IOptions<SecurityOptions>>().Value.Shell, sp.GetRequiredService<WorkspacePathPolicy>()));
        services.AddSingleton(sp => new ApprovalPolicy(sp.GetRequiredService<IOptions<SecurityOptions>>().Value.ApprovalPolicy));
        services.AddSingleton(sp => new SecretRedactor(sp.GetRequiredService<IOptions<FoundryOptions>>().Value.ApiKey));

        services.AddSingleton<RfpQueryService>();
        services.AddSingleton<CurrencyService>();
        services.AddSingleton<BidEvaluationService>();
        services.AddSingleton<ComplianceService>();
        services.AddSingleton<ClarificationService>();
        services.AddSingleton<AwardService>();
        return services;
    }
}
