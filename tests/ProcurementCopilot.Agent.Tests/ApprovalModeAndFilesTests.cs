using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ProcurementCopilot.Agent.Files;
using ProcurementCopilot.Agent.Middleware;
using ProcurementCopilot.Agent.Tools;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Testing;
using ProcurementCopilot.Testing.Fakes;
using Shouldly;
using static ProcurementCopilot.Testing.Script;

namespace ProcurementCopilot.Agent.Tests;

public class ModeGuardMiddlewareTests
{
    [Fact]
    public async Task Invoke_PlanMode_BlocksAndWritesNothing()
    {
        using var host = new AgentTestHost();
        AIFunction raw = host.Services.GetRequiredService<DraftClarificationEmailTool>().Create();
        var guarded = new ModeGuardMiddleware(raw, new StaticModeAccessor(AgentModes.Plan));

        string result = (await guarded.InvokeAsync(new AIFunctionArguments { ["vendorId"] = "VND-0005", ["subject"] = "s", ["body"] = "b" }))!.ToString()!;

        result.ShouldContain(ModeGuard.BlockedCode);
        result.ShouldContain("/mode execute");
        host.Outbox.Drafts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Invoke_ExecuteMode_RunsTheTool()
    {
        using var host = new AgentTestHost();
        AIFunction raw = host.Services.GetRequiredService<DraftClarificationEmailTool>().Create();
        var guarded = new ModeGuardMiddleware(raw, new StaticModeAccessor(AgentModes.Execute));

        await guarded.InvokeAsync(new AIFunctionArguments { ["vendorId"] = "VND-0005", ["subject"] = "s", ["body"] = "b" });

        host.Outbox.Drafts.Single().VendorId.ShouldBe("VND-0005");
    }

    [Fact]
    public async Task Run_HarnessInPlanMode_SideEffectToolIsBlockedAfterApproval()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient([Call("draft_clarification_email", Arg("vendorId", "VND-0005"), Arg("subject", "s"), Arg("body", "b")), Text("blocked?")]);
        HarnessAgent agent = host.CreateAgent(client);
        AgentSession session = await agent.CreateSessionAsync();

        AgentResponse first = await agent.RunAsync("draft it", session);
        ToolApprovalRequestContent request = first.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().Single();
        await agent.RunAsync(new ChatMessage(ChatRole.User, [request.CreateResponse(true)]), session);

        host.Outbox.Drafts.ShouldBeEmpty();
        host.Factory.HistoryProvider!.GetMessages(session).SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single().Result!.ToString()!.ShouldContain(ModeGuard.BlockedCode);
    }
}

public class ToolApprovalTests
{
    [Fact]
    public async Task Run_SideEffectingTool_SurfacesApprovalRequestThenExecutesAfterApproval()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient([Call("draft_clarification_email", Arg("vendorId", "VND-0005"), Arg("subject", "Clarify"), Arg("body", "Please confirm.")), Text("Draft saved.")]);
        (HarnessAgent agent, AgentSession session) = await AgentTestHost.InExecuteModeAsync(host.CreateAgent(client));

        AgentResponse first = await agent.RunAsync("draft it", session);
        ToolApprovalRequestContent request = first.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().Single();
        host.Outbox.Drafts.ShouldBeEmpty();
        AgentResponse second = await agent.RunAsync(new ChatMessage(ChatRole.User, [request.CreateResponse(true, "ok")]), session);

        ((FunctionCallContent)request.ToolCall).Name.ShouldBe(ToolNames.DraftClarificationEmail);
        second.Text.ShouldContain("Draft saved");
        host.Outbox.Drafts.Single().Subject.ShouldBe("Clarify");
        host.Audit.Actions.Single().Tool.ShouldBe(ToolNames.DraftClarificationEmail);
    }

    [Fact]
    public async Task Run_DeniedApproval_DoesNotExecuteTool()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient([Call("record_award_recommendation", Arg("rfpId", "RFP-2026-017"), Arg("vendorId", "VND-0003"), Arg("rationale", "best")), Text("Understood.")]);
        (HarnessAgent agent, AgentSession session) = await AgentTestHost.InExecuteModeAsync(host.CreateAgent(client));

        AgentResponse first = await agent.RunAsync("award", session);
        ToolApprovalRequestContent request = first.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().Single();
        await agent.RunAsync(new ChatMessage(ChatRole.User, [request.CreateResponse(false, "no")]), session);

        host.Outbox.Documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ReadOnlyTool_NeedsNoApproval()
    {
        using var host = new AgentTestHost();
        using var client = new ScriptedChatClient([Call("score_bid", Arg("rfpId", "RFP-2026-017"), Arg("bidId", "BID-001")), Text("82.56")]);
        (HarnessAgent agent, AgentSession session) = await AgentTestHost.InExecuteModeAsync(host.CreateAgent(client));

        AgentResponse response = await agent.RunAsync("score", session);

        response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ShouldBeEmpty();
        response.Text.ShouldContain("82.56");
    }
}

public class WorkspaceFileStoreTests
{
    [Fact]
    public async Task Store_EnforcesReadOnlyWritableAndExtensionRules()
    {
        using var host = new AgentTestHost();
        var store = new WorkspaceFileStore(host.Services.GetRequiredService<WorkspacePathPolicy>());

        (await store.ReadAsync("rfps/RFP-2026-017/rfp.md"))!.ShouldContain("RFP-2026-017");
        await store.WriteAsync("output/award-memo-RFP-2026-017.md", "# memo");
        (await store.FileExistsAsync("output/award-memo-RFP-2026-017.md")).ShouldBeTrue();
        await Should.ThrowAsync<UnauthorizedAccessException>(() => store.WriteAsync("rfps/RFP-2026-017/rfp.md", "tamper"));
        await Should.ThrowAsync<UnauthorizedAccessException>(() => store.WriteAsync("output/x.exe", "bin"));
        await Should.ThrowAsync<UnauthorizedAccessException>(() => store.ReadAsync("../data/vendors.json"));
        (await store.ListChildrenAsync(string.Empty)).ShouldNotBeEmpty();
    }
}
