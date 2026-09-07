using System.Text.Json;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Infrastructure.Audit;
using ProcurementCopilot.Infrastructure.Chat;
using ProcurementCopilot.Infrastructure.Data;
using ProcurementCopilot.Infrastructure.Outbox;
using ProcurementCopilot.Infrastructure.Sessions;
using ProcurementCopilot.Testing.Fakes;
using Shouldly;

namespace ProcurementCopilot.Application.Tests.Infrastructure;

public class FileSessionStoreTests
{
    [Fact]
    public async Task SaveLoadList_RoundTripsAndUsesGuidFileNames()
    {
        string dir = Path.Combine(Path.GetTempPath(), "pc-tests", Guid.NewGuid().ToString("N"));
        var sut = new FileSessionStore(dir, new FixedClock(DateTimeOffset.UtcNow));
        var id = Guid.NewGuid();
        using var doc = JsonDocument.Parse("{\"a\":1}");

        await sut.SaveAsync(id, doc.RootElement, "Evaluate RFP");
        JsonElement? loaded = await sut.LoadAsync(id);
        IReadOnlyList<SessionSummary> list = await sut.ListAsync();

        loaded!.Value.GetProperty("a").GetInt32().ShouldBe(1);
        list.Single().Title.ShouldBe("Evaluate RFP");
        Directory.GetFiles(dir).Select(Path.GetFileName).ShouldAllBe(f => f!.StartsWith(id.ToString("N")));
    }

    [Fact]
    public async Task Load_UnknownId_ReturnsNull()
    {
        var sut = new FileSessionStore(Path.Combine(Path.GetTempPath(), "pc-tests", Guid.NewGuid().ToString("N")), new FixedClock(DateTimeOffset.UtcNow));

        (await sut.LoadAsync(Guid.NewGuid())).ShouldBeNull();
        (await sut.ListAsync()).ShouldBeEmpty();
    }
}

public class OutboxAndAuditTests
{
    [Fact]
    public async Task SaveDraft_WritesUnderOutputOutbox()
    {
        WorkspacePathPolicy policy = TestPaths.Policy();
        var sut = new FileOutbox(policy);

        string path = await sut.SaveDraftAsync(new EmailDraft(Guid.NewGuid(), "VND-0005", "Subject", "Body", DateTimeOffset.UtcNow));

        path.ShouldStartWith("output/outbox/");
        File.ReadAllText(Path.Combine(policy.Root, path)).ShouldContain("status: DRAFT (not sent)");
    }

    [Fact]
    public async Task SaveDocument_OutsideOutput_IsRejected()
    {
        var sut = new FileOutbox(TestPaths.Policy());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.SaveDocumentAsync("../rfps/x.md", "x"));
    }

    [Fact]
    public async Task AuditLog_AppendsJsonLines()
    {
        WorkspacePathPolicy policy = TestPaths.Policy();
        var sut = new JsonlAuditLog(policy);

        await sut.RecordApprovalAsync(new ApprovalRecord(DateTimeOffset.UtcNow, "me", "shell", "hash", "approve-once", Guid.NewGuid()));
        await sut.RecordActionAsync(new ActionRecord(DateTimeOffset.UtcNow, "shell", "hash", "ok", null));

        File.ReadAllLines(Path.Combine(policy.Root, "output", "audit", "approvals.jsonl")).Length.ShouldBe(1);
        File.ReadAllLines(Path.Combine(policy.Root, "output", "audit", "actions.jsonl")).Single().ShouldContain("\"outcome\":\"ok\"");
    }
}

public class FoundryChatClientFactoryTests
{
    [Theory]
    [InlineData("https://contoso.openai.azure.com", true, "https://contoso.openai.azure.com/openai/v1/")]
    [InlineData("https://contoso.openai.azure.com/openai/v1", true, "https://contoso.openai.azure.com/openai/v1")]
    [InlineData("https://contoso.openai.azure.com/", false, "https://contoso.openai.azure.com/")]
    [InlineData("https://proj.services.ai.azure.com/api/projects/p1", true, "https://proj.services.ai.azure.com/api/projects/p1/openai/v1/")]
    public void BuildEndpoint_AppliesV1PathPerConfiguration(string endpoint, bool useV1, string expected)
    {
        FoundryChatClientFactory.BuildEndpoint(new FoundryOptions { Endpoint = endpoint, UseV1Path = useV1 }).ToString().ShouldBe(expected);
    }

    [Fact]
    public void CreateChatClient_MissingKey_NamesTheKeyNotTheValue()
    {
        var sut = new FoundryChatClientFactory(Microsoft.Extensions.Options.Options.Create(new FoundryOptions { Endpoint = "https://x.openai.azure.com", DeploymentName = "gpt" }));

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => sut.CreateChatClient());
        ex.Message.ShouldContain("Foundry:ApiKey");
    }
}

public class SeedDataStoreTests
{
    [Fact]
    public void Data_LoadsAndValidatesEverySeedFile()
    {
        SeedData data = new SeedDataStore(TestPaths.DataDirectory()).Data;

        data.Rfps.Count.ShouldBe(2);
        data.Vendors.Count.ShouldBe(5);
        data.Bids.Count.ShouldBe(5);
        data.Sanctions.Single().VendorId.Value.ShouldBe("VND-0004");
        data.FxRates.ShouldContain(r => r.From.Value == "USD" && r.Rate == 0.92m);
        data.Bids.Single(b => b.Id.Value == "BID-003").UnitPrice.Currency.Value.ShouldBe("USD");
    }
}
