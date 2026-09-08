using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ProcurementCopilot.Agent.Background;
using ProcurementCopilot.Agent.Shell;
using ProcurementCopilot.Agent.Skills;
using ProcurementCopilot.Agent.Tools;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Testing;
using Shouldly;

namespace ProcurementCopilot.Agent.Tests;

public class BackgroundAgentTests
{
    private static readonly string[] Forbidden = [ToolNames.DraftClarificationEmail, ToolNames.RecordAwardRecommendation, ToolNames.Shell];

    [Fact]
    public void Definitions_ChildToolSets_ContainNoSideEffectingFileOrShellTools()
    {
        using var host = new AgentTestHost();
        BackgroundAgentFactory factory = host.Services.GetRequiredService<BackgroundAgentFactory>();

        IReadOnlyList<BackgroundAgentDefinition> definitions = factory.Definitions(webSearchEnabled: true);

        definitions.Select(d => d.Name).ShouldBe(["market-research", "risk-analyst"]);
        foreach (BackgroundAgentDefinition definition in definitions)
        {
            definition.Tools.Select(t => t.Name).ShouldNotContain(n => Forbidden.Contains(n) || n.StartsWith("file_"));
            definition.Tools.OfType<ApprovalRequiredAIFunction>().ShouldBeEmpty();
        }

        definitions[0].Tools.Select(t => t.Name).ShouldBe([ToolNames.ConvertCurrency, new HostedWebSearchTool().Name]);
        definitions[1].Tools.Select(t => t.Name).ShouldBe([ToolNames.GetVendorProfile, ToolNames.CheckVendorCompliance]);
    }

    [Fact]
    public void Toolset_Get_RefusesSideEffectingToolsForChildren()
    {
        using var host = new AgentTestHost();

        Should.Throw<InvalidOperationException>(() => host.Services.GetRequiredService<ProcurementToolset>().Get(ToolNames.DraftClarificationEmail));
    }

    [Fact]
    public async Task TimeoutAgent_ClientNeverCompletes_ThrowsTimeout()
    {
        using var never = new NeverCompletingChatClient();
        var child = new ChatClientAgent(never, new ChatClientAgentOptions { Name = "slow" });
        AIAgent bounded = BackgroundAgentFactory.Wrap(child, new SemaphoreSlim(2, 2), TimeSpan.FromMilliseconds(200));

        await Should.ThrowAsync<TimeoutException>(() => bounded.RunAsync("go"));
        never.CallsStarted.ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrencyLimitedAgent_CapsParallelRuns()
    {
        int running = 0, peak = 0;
        var gate = new SemaphoreSlim(2, 2);
        AIAgent Make() => new ConcurrencyLimitedAgent(new ChatClientAgent(new ProbeClient(() =>
        {
            int now = Interlocked.Increment(ref running);
            peak = Math.Max(peak, now);
            Thread.Sleep(60);
            Interlocked.Decrement(ref running);
        }), new ChatClientAgentOptions { Name = "p" }), gate);

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Make().RunAsync("x")));

        peak.ShouldBeLessThanOrEqualTo(2);
    }

    private sealed class ProbeClient(Action onCall) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            onCall();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}

public class SkillsDiscoveryTests
{
    [Fact]
    public async Task Source_ConfiguredPath_DiscoversExactlyThreeSkills()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient();
        HarnessAgent agent = host.CreateAgent(client);
        AgentSession session = await agent.CreateSessionAsync();
        using AgentFileSkillsSource source = SkillsSourceFactory.Create(host.Options.SkillsPath);

        IList<AgentSkill> skills = await source.GetSkillsAsync(new AgentSkillsSourceContext(agent, session));

        skills.Select(s => s.Frontmatter.Name).Order().ShouldBe(["award-memo", "compliance-check", "database-schema", "rfp-scoring"]);
    }

    [Fact]
    public void Create_CurrentWorkingDirectory_IsRefused()
    {
        Should.Throw<InvalidOperationException>(() => SkillsSourceFactory.Create(Directory.GetCurrentDirectory()));
    }
}

public class ConfinedShellToolTests
{
    private static ConfinedShellTool Tool(AgentTestHost host) => host.Services.GetRequiredService<ConfinedShellTool>();

    [Fact]
    public async Task Execute_DeniedCommand_ReturnsErrorCodeWithoutRunning()
    {
        using var host = new AgentTestHost();

        string result = await Tool(host).ExecuteAsync("cat /etc/passwd", CancellationToken.None);

        result.ShouldContain("Shell.AbsolutePathNotAllowed");
        host.Shell.Commands.ShouldBeEmpty();
        host.Audit.Actions.Single().Outcome.ShouldBe("denied");
    }

    [Fact]
    public async Task Execute_AllowedCommand_RunsAndFormatsOutput()
    {
        using var host = new AgentTestHost();
        host.Shell.Stdout = "rfp.md";

        string result = await Tool(host).ExecuteAsync("ls rfps/RFP-2026-017", CancellationToken.None);

        result.ShouldContain("exit_code: 0");
        result.ShouldContain("rfp.md");
        host.Shell.Commands.ShouldBe(["ls rfps/RFP-2026-017"]);
    }

    [Fact]
    public async Task Execute_Timeout_ReturnsTimeoutError()
    {
        using var host = new AgentTestHost(c => c["Security:Shell:TimeoutSeconds"] = "1");
        host.Shell.Delay = TimeSpan.FromSeconds(5);

        string result = await Tool(host).ExecuteAsync("ls", CancellationToken.None);

        result.ShouldContain("Shell.Timeout");
        host.Audit.Actions.Single().Outcome.ShouldBe("timeout");
    }

    [Fact]
    public async Task Execute_HugeOutput_IsTruncatedToCap()
    {
        using var host = new AgentTestHost(c => c["Security:Shell:MaxOutputBytes"] = "1024");
        host.Shell.Stdout = new string('a', 5000);

        string result = await Tool(host).ExecuteAsync("cat rfps/RFP-2026-017/rfp.md", CancellationToken.None);

        result.ShouldContain("output truncated to 1024 bytes");
        result.ShouldContain(new string('a', 1024));
        result.ShouldNotContain(new string('a', 1025));
    }

    [Fact]
    public void Create_ToolIsNamedShellAndDescribesAllowlist()
    {
        using var host = new AgentTestHost();

        AIFunction function = Tool(host).Create();

        function.Name.ShouldBe(ToolNames.Shell);
        function.Description.ShouldContain("ls, dir, cat");
        ShellOptions.DefaultAllowedCommands.Count.ShouldBe(12);
    }
}
