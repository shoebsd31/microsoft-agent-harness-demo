using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProcurementCopilot.Agent.DependencyInjection;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.DependencyInjection;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Infrastructure.Data;
using ProcurementCopilot.Infrastructure.Outbox;
using ProcurementCopilot.Infrastructure.Repositories;
using ProcurementCopilot.Testing.Fakes;

namespace ProcurementCopilot.Agent.Tests;

/// <summary>Builds the real Application + Agent DI graph over the seed data, with in-memory fakes for every side effect.</summary>
public sealed class AgentTestHost : IDisposable
{
    public AgentTestHost(Action<Dictionary<string, string?>>? configure = null)
    {
        WorkspaceRoot = Path.Combine(Path.GetTempPath(), "pc-agent-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(WorkspaceRoot, "rfps", "RFP-2026-017"));
        Directory.CreateDirectory(Path.Combine(WorkspaceRoot, "output"));
        File.WriteAllText(Path.Combine(WorkspaceRoot, "rfps", "RFP-2026-017", "rfp.md"), "# RFP-2026-017");

        var overrides = new Dictionary<string, string?> { ["Workspace:Root"] = WorkspaceRoot };
        configure?.Invoke(overrides);
        Configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .AddInMemoryCollection(overrides)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices(Configuration);
        services.AddAgentServices(Configuration);
        var store = new SeedDataStore(DataDirectory());
        services.AddSingleton<IRfpRepository>(new JsonRfpRepository(store));
        services.AddSingleton<IVendorRepository>(new JsonVendorRepository(store));
        services.AddSingleton<IBidRepository>(new JsonBidRepository(store));
        services.AddSingleton<ISanctionsRepository>(new CsvSanctionsRepository(store));
        services.AddSingleton<IFxRateRepository>(new JsonFxRateRepository(store));
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<ISessionStore>(Sessions);
        services.AddSingleton<IOutbox>(Outbox);
        services.AddSingleton<IAuditLog>(Audit);
        services.AddSingleton<IShellExecutor>(Shell);
        services.AddSingleton<IAwardRecorder>(sp => new FileAwardRecorder(sp.GetRequiredService<IOutbox>()));
        services.AddSingleton<IReadOnlyQueryExecutor>(Query);
        services.AddSingleton<IDataProviderInfo>(new FakeDataProviderInfo("Json", "test seed"));
        Services = services.BuildServiceProvider();
    }

    public string WorkspaceRoot { get; }

    public IConfiguration Configuration { get; }

    public ServiceProvider Services { get; }

    public InMemorySessionStore Sessions { get; } = new();

    public InMemoryOutbox Outbox { get; } = new();

    public InMemoryAuditLog Audit { get; } = new();

    public FixedClock Clock { get; } = new(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));

    public FakeShellExecutor Shell { get; } = new();

    public FakeQueryExecutor Query { get; } = new();

    public HarnessAgentFactory Factory => Services.GetRequiredService<HarnessAgentFactory>();

    public ProcurementAgentOptions Options => Services.GetRequiredService<ProcurementAgentOptions>() with
    {
        SkillsPath = Path.Combine(AppContext.BaseDirectory, "skills"),
        FileMemoryRoot = Path.Combine(WorkspaceRoot, "agent-file-memory"),
    };

    public HarnessAgent CreateAgent(IChatClient chatClient, IChatClient? judge = null, IChatClient? background = null) =>
        Factory.Create(chatClient, Options, judge, background);

    public static async Task<(HarnessAgent Agent, AgentSession Session)> InExecuteModeAsync(HarnessAgent agent)
    {
        AgentSession session = await agent.CreateSessionAsync();
        await agent.GetService<AgentModeProvider>()!.SetModeAsync(session, AgentModes.Execute);
        return (agent, session);
    }

    public static string DataDirectory()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "rfps.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("data directory not found");
    }

    public void Dispose() => Services.Dispose();
}
