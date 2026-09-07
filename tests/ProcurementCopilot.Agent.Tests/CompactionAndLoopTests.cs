using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent.Compaction;
using ProcurementCopilot.Agent.State;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Testing;
using Shouldly;
using static ProcurementCopilot.Testing.Script;

namespace ProcurementCopilot.Agent.Tests;

public class CompactionStrategyTests
{
    private static List<ChatMessage> LongConversation()
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, "You are the Procurement Copilot.") };
        messages.Add(new ChatMessage(ChatRole.User, "Evaluate RFP-2026-017 and recommend a vendor."));
        for (int i = 0; i < 12; i++)
        {
            messages.Add(new ChatMessage(ChatRole.User, $"Filler question {i}: " + Prose(i, 60)));
            messages.Add(new ChatMessage(ChatRole.Assistant, $"Filler answer {i}: " + Prose(i + 100, 60)));
        }

        FunctionCallContent score = CallContent("score_bid", [Arg("rfpId", "RFP-2026-017"), Arg("bidId", "BID-003")]);
        messages.Add(new ChatMessage(ChatRole.Assistant, [score]));
        messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(score.CallId, "{\"bidId\":\"BID-003\",\"weightedTotal\":89.60}")]));
        var approval = new ToolApprovalRequestContent("req-1", CallContent("draft_clarification_email", [Arg("vendorId", "VND-0005")]));
        messages.Add(new ChatMessage(ChatRole.Assistant, [approval]));
        messages.Add(new ChatMessage(ChatRole.User, [approval.CreateResponse(true, "approved")]));
        for (int i = 0; i < 6; i++)
        {
            messages.Add(new ChatMessage(ChatRole.User, $"Late filler {i}: " + Prose(i + 200, 50)));
            messages.Add(new ChatMessage(ChatRole.Assistant, "ok " + Prose(i + 300, 25)));
        }

        return messages;
    }

    private static string Prose(int seed, int words) =>
        string.Join(' ', Enumerable.Range(0, words).Select(w => $"vendor{(seed * 7 + w * 13) % 97} quoted {(seed + w) * 31 % 1000} units"));

    [Fact]
    public async Task Compact_OverSmallBudget_KeepsProtectedFactsAndFitsBudget()
    {
        var strategy = new ProcurementCompactionStrategy(inputBudgetTokens: 2_000);
        List<ChatMessage> input = LongConversation();

        List<ChatMessage> retained = (await CompactionProvider.CompactAsync(strategy, input)).ToList();

        strategy.CompactionCount.ShouldBe(1);
        retained.Count.ShouldBeLessThan(input.Count);
        retained.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ShouldContain(c => c.Name == "score_bid");
        retained.SelectMany(m => m.Contents).OfType<FunctionResultContent>().ShouldContain(r => r.Result!.ToString()!.Contains("89.60"));
        retained.SelectMany(m => m.Contents).OfType<ToolApprovalResponseContent>().Count().ShouldBe(1);
        retained.First(m => m.Role == ChatRole.System).Text.ShouldContain("Procurement Copilot");
        ChatMessage summary = retained.Single(m => m.AdditionalProperties?.ContainsKey(CompactionMessageGroup.SummaryPropertyKey) == true);
        summary.Text.ShouldContain("RFP-2026-017");
        summary.Text.ShouldContain("Evaluate RFP-2026-017");
        strategy.LastIncludedTokenCount.ShouldBeLessThanOrEqualTo(2_000);
        strategy.LastIncludedTokenCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Compact_UnderBudget_DoesNothing()
    {
        var strategy = new ProcurementCompactionStrategy(inputBudgetTokens: 200_000);
        List<ChatMessage> input = LongConversation();

        (await CompactionProvider.CompactAsync(strategy, input)).Count().ShouldBe(input.Count);
        strategy.CompactionCount.ShouldBe(0);
    }

    [Fact]
    public void Constructor_FromWindowAndOutput_ComputesInputBudget()
    {
        new ProcurementCompactionStrategy(128_000, 16_384).InputBudgetTokens.ShouldBe(111_616);
    }
}

public class LoopEvaluatorTests
{
    private static async Task<(AgentTestHost Host, HarnessAgent Agent, AgentSession Session)> ExecuteSessionAsync(int scored)
    {
        var host = new AgentTestHost();
        using var client = new ScriptedChatClient();
        (HarnessAgent agent, AgentSession session) = await AgentTestHost.InExecuteModeAsync(host.CreateAgent(client));
        var state = new EvaluationState { ActiveRfpId = "RFP-2026-017" };
        foreach (string bid in new[] { "BID-001", "BID-002", "BID-003", "BID-004", "BID-005" }.Take(scored))
        {
            state.Scores[bid] = new PersistedScore { BidId = bid, VendorId = "VND-0001", WeightedTotal = 1 };
        }

        SessionEvaluationStateStore.Write(session, state);
        return (host, agent, session);
    }

    [Fact]
    public async Task Evaluate_FourOfFiveScored_Continues()
    {
        (AgentTestHost host, HarnessAgent agent, AgentSession session) = await ExecuteSessionAsync(4);
        using (host)
        {
            LoopEvaluation evaluation = await host.Factory.LoopEvaluator!.EvaluateAsync(new LoopContext(agent, session, [], new AgentResponse(new ChatMessage(ChatRole.Assistant, "partial"))));

            evaluation.ShouldReinvoke.ShouldBeTrue();
            evaluation.Feedback!.ShouldContain("BID-005");
        }
    }

    [Fact]
    public async Task Evaluate_AllScored_Stops()
    {
        (AgentTestHost host, HarnessAgent agent, AgentSession session) = await ExecuteSessionAsync(5);
        using (host)
        {
            (await host.Factory.LoopEvaluator!.EvaluateAsync(new LoopContext(agent, session, [], new AgentResponse(new ChatMessage(ChatRole.Assistant, "done"))))).ShouldReinvoke.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Evaluate_PlanMode_NeverLoops()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient();
        HarnessAgent agent = host.CreateAgent(client);
        AgentSession session = await agent.CreateSessionAsync();
        SessionEvaluationStateStore.Write(session, new EvaluationState { ActiveRfpId = "RFP-2026-017" });

        (await host.Factory.LoopEvaluator!.EvaluateAsync(new LoopContext(agent, session, [], new AgentResponse(new ChatMessage(ChatRole.Assistant, "plan"))))).ShouldReinvoke.ShouldBeFalse();
    }

    [Fact]
    public async Task Run_AgentNeverFinishes_StopsAtMaxIterations()
    {
        using var host = new AgentTestHost(c => c["Agent:MaxLoopIterations"] = "3");
        using var client = new ScriptedChatClient([Call("get_rfp", Arg("rfpId", "RFP-2026-017"))]) { Fallback = (_, _) => Text("Still thinking, not scoring anything.") };
        (HarnessAgent agent, AgentSession session) = await AgentTestHost.InExecuteModeAsync(host.CreateAgent(client));

        AgentResponse response = await agent.RunAsync("Evaluate RFP-2026-017", session);

        client.CallCount.ShouldBe(4); // 3 loop iterations: iteration 1 = tool call + text (2 model calls), iterations 2 and 3 = 1 text each
        response.Text.ShouldContain("Still thinking");
        (await host.Factory.LoopEvaluator!.OutstandingAsync(session)).Count.ShouldBe(5);
    }
}
