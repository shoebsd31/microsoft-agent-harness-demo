using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProcurementCopilot.Agent.Background;
using ProcurementCopilot.Agent.Middleware;
using ProcurementCopilot.Agent.Prompts;
using ProcurementCopilot.Agent.Shell;
using ProcurementCopilot.Agent.State;
using ProcurementCopilot.Agent.Tools;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Configuration;

namespace ProcurementCopilot.Agent.DependencyInjection;

/// <summary>Registers the harness composition: tools, providers, evaluators, background agents and the factory.</summary>
public static class AgentServiceExtensions
{
    /// <summary>Adds agent services. Requires <c>AddApplicationServices</c> and <c>AddInfrastructureServices</c>.</summary>
    public static IServiceCollection AddAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<PromptCatalog>();
        services.AddSingleton<IAgentModeAccessor, RunContextModeAccessor>();
        services.AddSingleton<IEvaluationStateStore, SessionEvaluationStateStore>();

        services.AddSingleton<ListOpenRfpsTool>();
        services.AddSingleton<GetRfpTool>();
        services.AddSingleton<ListBidsTool>();
        services.AddSingleton<GetVendorProfileTool>();
        services.AddSingleton<ConvertCurrencyTool>();
        services.AddSingleton<ScoreBidTool>();
        services.AddSingleton<CheckVendorComplianceTool>();
        services.AddSingleton<DraftClarificationEmailTool>();
        services.AddSingleton<RecordAwardRecommendationTool>();
        services.AddSingleton<ProcurementToolset>();

        services.AddSingleton<IShellExecutor>(sp => new LocalShellRunner(
            sp.GetRequiredService<Application.Security.WorkspacePathPolicy>(),
            sp.GetRequiredService<Application.Security.ShellCommandPolicy>(),
            sp.GetRequiredService<IOptions<SecurityOptions>>().Value.Shell));
        services.AddSingleton(sp => new ConfinedShellTool(
            sp.GetRequiredService<Application.Security.ShellCommandPolicy>(),
            sp.GetRequiredService<IShellExecutor>(),
            sp.GetRequiredService<IOptions<SecurityOptions>>().Value.Shell,
            sp.GetRequiredService<IAuditLog>(),
            sp.GetRequiredService<IClock>()));

        services.AddSingleton<BackgroundAgentFactory>();
        services.AddSingleton(sp => ProcurementAgentOptions.FromConfiguration(
            sp.GetRequiredService<IOptions<AgentOptions>>(),
            sp.GetRequiredService<IOptions<SecurityOptions>>(),
            sp.GetRequiredService<IOptions<WorkspaceOptions>>()));
        services.AddSingleton<HarnessAgentFactory>();
        return services;
    }
}
