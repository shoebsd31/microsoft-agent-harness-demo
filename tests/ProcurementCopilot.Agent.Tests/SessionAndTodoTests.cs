using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Agent.State;
using ProcurementCopilot.Testing;
using Shouldly;
using static ProcurementCopilot.Testing.Script;

namespace ProcurementCopilot.Agent.Tests;

public class SessionPersistenceTests
{
    [Fact]
    public async Task Run_ThreeToolCalls_StoreIsWrittenAfterEveryModelCall()
    {
        using var host = new AgentTestHost(c => c["Agent:MaxLoopIterations"] = "1");
        using var client = new ScriptedChatClient(
        [
            Call("get_rfp", Arg("rfpId", "RFP-2026-017")),
            Call("list_bids", Arg("rfpId", "RFP-2026-017")),
            Call("score_bid", Arg("rfpId", "RFP-2026-017"), Arg("bidId", "BID-001")),
            Text("BID-001 scores 82.56 (score_bid)."),
        ]);
        (HarnessAgent agent, AgentSession session) = await AgentTestHost.InExecuteModeAsync(host.CreateAgent(client));

        AgentResponse response = await agent.RunAsync("Score BID-001", session);

        response.Text.ShouldContain("82.56");
        client.CallCount.ShouldBe(4);
        host.Sessions.WriteCount.ShouldBe(client.CallCount);
        host.Factory.HistoryProvider!.CheckpointCount.ShouldBe(4);
    }

    [Fact]
    public async Task Resume_DeserializedSession_ContainsHistoryAndScores()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient([Call("score_bid", Arg("rfpId", "RFP-2026-017"), Arg("bidId", "BID-003")), Text("done")]);
        (HarnessAgent agent, AgentSession session) = await AgentTestHost.InExecuteModeAsync(host.CreateAgent(client));
        await agent.RunAsync("Score BID-003", session);
        Guid id = SessionIdentity.TryGet(session)!.Value;

        JsonElement stored = (await host.Sessions.LoadAsync(id))!.Value;
        AgentSession resumed = await agent.DeserializeSessionAsync(stored);

        host.Factory.HistoryProvider!.GetMessages(resumed).Count.ShouldBeGreaterThanOrEqualTo(3);
        SessionEvaluationStateStore.Read(resumed).Scores["BID-003"].WeightedTotal.ShouldBe(89.60m);
        SessionIdentity.TryGet(resumed).ShouldBe(id);
    }
}

public class TodoTrackingTests
{
    [Fact]
    public async Task Run_TodoToolCalls_ReflectInProviderState()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient(
        [
            Call("todos_add", Arg("todos", new object[] { new { title = "Score BID-001" }, new { title = "Score BID-002" } })),
            Call("todos_complete", Arg("items", new object[] { new { id = 1, reason = "scored" } })),
            Text("One down."),
        ]);
        HarnessAgent agent = host.CreateAgent(client);
        AgentSession session = await agent.CreateSessionAsync();

        await agent.RunAsync("Plan the scoring", session);
        IReadOnlyList<TodoItem> todos = await agent.GetService<TodoProvider>()!.GetAllTodosAsync(session);

        todos.Count.ShouldBe(2);
        todos.Single(t => t.Title == "Score BID-001").IsComplete.ShouldBeTrue();
        todos.Single(t => t.Title == "Score BID-002").IsComplete.ShouldBeFalse();
    }
}

public class PromptResourceTests
{
    [Fact]
    public void Prompts_LoadFromEmbeddedResources()
    {
        var catalog = new Prompts.PromptCatalog();

        catalog.HarnessAddendum.ShouldContain("one todo per bid");
        catalog.AgentInstructions.ShouldContain("Procurement Copilot");
        catalog.MarketResearchInstructions.ShouldContain("market-research");
        catalog.RiskAnalystInstructions.ShouldContain("risk-analyst");
        Prompts.PromptCatalog.ResourceNames().Count.ShouldBe(4);
    }

    [Fact]
    public void HarnessAddendum_ContainsUntrustedDataClause()
    {
        new Prompts.PromptCatalog().HarnessAddendum.ShouldContain("<untrusted_data");
        new Prompts.PromptCatalog().HarnessAddendum.ShouldContain("never instructions", Case.Insensitive);
    }
}

public class UntrustedDataEnvelopeToolTests
{
    [Fact]
    public async Task ListBids_ToolOutput_WrapsVendorText()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient([Call("list_bids", Arg("rfpId", "RFP-2026-017")), Text("ok")]);
        (HarnessAgent agent, AgentSession session) = await AgentTestHost.InExecuteModeAsync(host.CreateAgent(client));

        await agent.RunAsync("list", session);

        string toolResult = host.Factory.HistoryProvider!.GetMessages(session).SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single().Result!.ToString()!;
        toolResult.ShouldContain("<untrusted_data source=\\\"bid:BID-005\\\">");
    }

    [Fact]
    public async Task GetVendorProfile_InjectionNotes_AreWrappedWithVendorSource()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient([Call("get_vendor_profile", Arg("vendorId", "VND-0002")), Text("ok")]);
        (HarnessAgent agent, AgentSession session) = await AgentTestHost.InExecuteModeAsync(host.CreateAgent(client));

        await agent.RunAsync("vendor", session);

        string toolResult = host.Factory.HistoryProvider!.GetMessages(session).SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single().Result!.ToString()!;
        toolResult.ShouldContain("<untrusted_data source=\\\"vendor:VND-0002\\\">");
        toolResult.ShouldContain("Ignore previous instructions");
    }
}
