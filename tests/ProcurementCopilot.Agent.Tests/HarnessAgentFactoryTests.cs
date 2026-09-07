using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent.Compaction;
using ProcurementCopilot.Agent.Looping;
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Agent.Telemetry;
using ProcurementCopilot.Agent.Tools;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Testing;
using Shouldly;

namespace ProcurementCopilot.Agent.Tests;

public class HarnessAgentFactoryTests
{
    [Fact]
    public void BuildOptions_FromAppSettings_SetsEveryCapabilitySwitchAsSpecified()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient(supportsWebSearch: true);

        HarnessAgentOptions o = host.Factory.BuildOptions(client, host.Options);

        o.MaximumIterationsPerRequest.ShouldBe(15);                       // §4.1 from Agent:MaxFunctionInvocationIterations
        o.ChatHistoryProvider.ShouldBeOfType<CheckpointingChatHistoryProvider>(); // §4.2
        o.MaxContextWindowTokens.ShouldBe(128_000);                       // §4.3
        o.MaxOutputTokens.ShouldBe(16_384);
        o.CompactionStrategy.ShouldBeOfType<ProcurementCompactionStrategy>();
        o.DisableCompaction.ShouldBeFalse();
        o.DisableTodoProvider.ShouldBeFalse();                            // §4.4
        o.DisableAgentModeProvider.ShouldBeFalse();                       // §4.5
        o.AgentModeProviderOptions!.DefaultMode.ShouldBe(AgentModes.Plan);
        o.DisableFileMemory.ShouldBeFalse();                              // §4.6
        o.FileAccessStore.ShouldNotBeNull();
        o.DisableToolAutoApproval.ShouldBeFalse();                        // §4.7
        o.ToolApprovalAgentOptions!.AutoApprovalRules.ShouldNotBeEmpty();
        o.DisableOpenTelemetry.ShouldBeFalse();                           // §4.8
        o.OpenTelemetrySourceName.ShouldBe(AgentTelemetry.SourceName);
        o.DisableWebSearch.ShouldBeFalse();                               // §4.9 (client supports it)
        o.DisableAgentSkillsProvider.ShouldBeFalse();                     // §4.10
        o.AgentSkillsSource.ShouldBeOfType<AgentFileSkillsSource>();
        o.BackgroundAgents!.Select(a => a.Name).ShouldBe(["market-research", "risk-analyst"]); // §4.11
        o.BackgroundAgentsProviderOptions!.WaitTimeout.ShouldBe(TimeSpan.FromSeconds(120));
        o.ChatOptions!.Tools!.ShouldContain(t => t.Name == ToolNames.Shell);   // §4.12
        o.LoopEvaluators!.OfType<AllBidsScoredEvaluator>().Count().ShouldBe(1); // §4.13
        o.LoopAgentOptions!.MaxIterations.ShouldBe(5);
        o.HarnessInstructions.ShouldStartWith(HarnessAgent.DefaultInstructions);
        o.HarnessInstructions.ShouldContain("untrusted_data");
    }

    [Fact]
    public void BuildOptions_ClientWithoutHostedSearch_DisablesWebSearchInsteadOfCrashing()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient(supportsWebSearch: false);

        host.Factory.BuildOptions(client, host.Options).DisableWebSearch.ShouldBeTrue();
    }

    [Fact]
    public void BuildOptions_ConfigOverride_FlowsToIterationLimitAndLoopBound()
    {
        using var host = new AgentTestHost(c =>
        {
            c["Agent:MaxFunctionInvocationIterations"] = "7";
            c["Agent:MaxLoopIterations"] = "3";
            c["Agent:EnableWebSearch"] = "false";
        });
        using var client = new ScriptedChatClient(supportsWebSearch: true);

        HarnessAgentOptions o = host.Factory.BuildOptions(client, host.Options);

        o.MaximumIterationsPerRequest.ShouldBe(7);
        o.LoopAgentOptions!.MaxIterations.ShouldBe(3);
        o.DisableWebSearch.ShouldBeTrue();
    }

    [Fact]
    public void BuildOptions_JudgeEnabledWithClient_AddsJudgeEvaluator()
    {
        using var host = new AgentTestHost(c => c["Agent:EnableJudge"] = "true");
        using var client = new ScriptedChatClient();
        using var judge = new ScriptedChatClient();

        host.Factory.BuildOptions(client, host.Options, judge).LoopEvaluators!.OfType<AIJudgeLoopEvaluator>().Count().ShouldBe(1);
    }

    [Fact]
    public void BuildOptions_SideEffectingTools_AreApprovalRequired()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient();

        IList<AITool> tools = host.Factory.BuildOptions(client, host.Options).ChatOptions!.Tools!;

        tools.Single(t => t.Name == ToolNames.DraftClarificationEmail).ShouldBeOfType<ApprovalRequiredAIFunction>();
        tools.Single(t => t.Name == ToolNames.RecordAwardRecommendation).ShouldBeOfType<ApprovalRequiredAIFunction>();
        tools.Single(t => t.Name == ToolNames.Shell).ShouldBeOfType<ApprovalRequiredAIFunction>();
        tools.Single(t => t.Name == ToolNames.ScoreBid).ShouldNotBeOfType<ApprovalRequiredAIFunction>();
    }

    [Fact]
    public async Task Create_ProducesHarnessAgentInPlanModeWithProviders()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient();

        HarnessAgent agent = host.CreateAgent(client);
        AgentSession session = await agent.CreateSessionAsync();

        (await agent.GetService<AgentModeProvider>()!.GetModeAsync(session)).ShouldBe(AgentModes.Plan);
        agent.GetService<TodoProvider>().ShouldNotBeNull();
        agent.GetService<BackgroundAgentsProvider>().ShouldNotBeNull();
    }
}
