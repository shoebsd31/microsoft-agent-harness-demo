using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProcurementCopilot.Agent;
using ProcurementCopilot.Agent.DependencyInjection;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.DependencyInjection;
using ProcurementCopilot.Infrastructure.DependencyInjection;
using Shouldly;

namespace ProcurementCopilot.Integration.Tests;

/// <summary>One end-to-end run of the first demo prompt against the live Foundry deployment. Skipped without credentials.</summary>
public class FoundrySmokeTests
{
    [FoundryFact]
    public async Task FirstPrompt_PlanMode_ProducesPlanAndTodos()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "pc-integration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(workspace, "rfps"));
        Directory.CreateDirectory(Path.Combine(workspace, "output"));
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Workspace:Root"] = workspace, ["Sessions:Directory"] = Path.Combine(workspace, "sessions") })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices(configuration);
        services.AddInfrastructureServices(configuration, Path.Combine(workspace, "logs"));
        services.AddAgentServices(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        IChatClient chat = provider.GetRequiredService<IChatClientFactory>().CreateChatClient();
        HarnessAgentFactory factory = provider.GetRequiredService<HarnessAgentFactory>();
        HarnessAgent agent = factory.Create(chat, provider.GetRequiredService<ProcurementAgentOptions>() with { SkillsPath = Path.Combine(AppContext.BaseDirectory, "skills") });
        AgentSession session = await agent.CreateSessionAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        AgentResponse response = await agent.RunAsync("Evaluate RFP-2026-017 and recommend a vendor.", session, cancellationToken: cts.Token);

        response.Text.ShouldNotBeNullOrWhiteSpace();
        (await agent.GetService<TodoProvider>()!.GetAllTodosAsync(session, cts.Token)).Count.ShouldBeGreaterThan(0);
        (await agent.GetService<AgentModeProvider>()!.GetModeAsync(session, cts.Token)).ShouldBe(AgentModes.Plan);
    }
}
