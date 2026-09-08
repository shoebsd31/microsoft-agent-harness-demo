using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ProcurementCopilot.Agent.Tools;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Testing;
using Shouldly;
using static ProcurementCopilot.Testing.Script;

namespace ProcurementCopilot.Agent.Tests;

public class QueryReadOnlyToolTests
{
    private static QueryReadOnlyTool Tool(AgentTestHost host) => host.Services.GetRequiredService<QueryReadOnlyTool>();

    [Fact]
    public async Task Execute_AllowedSelect_ReturnsRowsAndAudits()
    {
        using var host = new AgentTestHost();
        host.Query.IsAvailable = true;

        string result = await Tool(host).ExecuteAsync("SELECT TOP 5 VendorId, VendorName FROM copilot.Vendors", CancellationToken.None);

        result.ShouldContain("\"columns\":[\"VendorId\",\"VendorName\"]");
        result.ShouldContain("Trikes, Inc.");
        result.ShouldContain("\"rowCount\":2");
        host.Query.Queries.ShouldBe(["SELECT TOP 5 VendorId, VendorName FROM copilot.Vendors"]);
        host.Audit.Actions.Single().Outcome.ShouldBe("ok");
    }

    [Fact]
    public async Task Execute_DeniedByPolicy_NeverReachesTheDatabase()
    {
        using var host = new AgentTestHost();
        host.Query.IsAvailable = true;

        string result = await Tool(host).ExecuteAsync("SELECT * FROM Purchasing.Vendor", CancellationToken.None);

        result.ShouldContain("Sql.SchemaNotAllowed");
        host.Query.Queries.ShouldBeEmpty();
        host.Audit.Actions.Single().Outcome.ShouldBe("denied");
    }

    [Fact]
    public async Task Execute_TruncatedResult_ReportsTruncation()
    {
        using var host = new AgentTestHost(c => c["SqlServer:QueryRowLimit"] = "1");
        host.Query.IsAvailable = true;

        string result = await Tool(host).ExecuteAsync("SELECT VendorId FROM copilot.Vendors", CancellationToken.None);

        result.ShouldContain("\"truncated\":true");
        result.ShouldContain("truncated to 1 rows");
    }

    [Fact]
    public async Task Execute_DatabaseError_IsReturnedAsToolError()
    {
        using var host = new AgentTestHost();
        host.Query.IsAvailable = true;
        host.Query.Error = new Error("Sql.Error", "permission denied");

        string result = await Tool(host).ExecuteAsync("SELECT VendorId FROM copilot.Vendors", CancellationToken.None);

        result.ShouldContain("Sql.Error");
        host.Audit.Actions.Single().Outcome.ShouldBe("Sql.Error");
    }

    [Fact]
    public void Toolset_OffersQueryToolOnlyWhenADatabaseIsAvailable()
    {
        using var without = new AgentTestHost();
        using var client = new ScriptedChatClient();
        without.Factory.BuildOptions(client, without.Options).ChatOptions!.Tools!.ShouldNotContain(t => t.Name == ToolNames.QueryReadOnly);

        using var with = new AgentTestHost();
        with.Query.IsAvailable = true;
        IList<AITool> tools = with.Factory.BuildOptions(client, with.Options).ChatOptions!.Tools!;
        tools.Single(t => t.Name == ToolNames.QueryReadOnly).ShouldBeOfType<ApprovalRequiredAIFunction>();
    }

    [Fact]
    public async Task Run_QueryTool_RequiresApprovalThenExecutes()
    {
        using var host = new AgentTestHost();
        host.Query.IsAvailable = true;
        using var client = new ScriptedChatClient([Call("query_readonly", Arg("sql", "SELECT TOP 2 VendorId, VendorName FROM copilot.Vendors")), Text("Two vendors found.")]);
        (HarnessAgent agent, AgentSession session) = await AgentTestHost.InExecuteModeAsync(host.CreateAgent(client));

        AgentResponse first = await agent.RunAsync("list vendors", session);
        ToolApprovalRequestContent request = first.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().Single();
        host.Query.Queries.ShouldBeEmpty();
        AgentResponse second = await agent.RunAsync(new ChatMessage(ChatRole.User, [request.CreateResponse(true)]), session);

        second.Text.ShouldContain("Two vendors found");
        host.Query.Queries.Count.ShouldBe(1);
    }
}
