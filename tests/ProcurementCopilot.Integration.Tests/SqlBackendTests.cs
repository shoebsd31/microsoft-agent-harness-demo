using Dapper;
using Microsoft.Data.SqlClient;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Scoring;
using ProcurementCopilot.Domain.ValueObjects;
using ProcurementCopilot.Infrastructure.SqlServer;
using Shouldly;

namespace ProcurementCopilot.Integration.Tests;

/// <summary>Runs against the configured AdventureWorks2019 instance; skipped when it is unreachable or not migrated.</summary>
public class SqlBackendTests
{
    private static SqlConnectionFactory Factory => new(SqlServerTestConfig.Options);

    [SqlServerFact]
    public async Task Repositories_LoadTheSeededScenario()
    {
        var rfps = new SqlRfpRepository(Factory);
        var bids = new SqlBidRepository(Factory);
        var vendors = new SqlVendorRepository(Factory);

        Rfp rfp = (await rfps.GetByIdAsync(RfpId.Create("RFP-2026-017").Value))!;
        IReadOnlyList<Bid> rfpBids = await bids.GetByRfpAsync(rfp.Id);
        Vendor proseware = (await vendors.GetByIdAsync(VendorId.Create("VND-1678").Value))!;

        rfp.Currency.Value.ShouldBe("USD");
        rfp.RequiredCertifications.ShouldBe(["ISO 4210", "ISO 9001"]);
        rfpBids.Count.ShouldBe(5);
        rfpBids.Single(b => b.Id.Value == "BID-003").UnitPrice.Currency.Value.ShouldBe("EUR");
        proseware.IsActive.ShouldBeFalse();
        proseware.CreditRating.ShouldBe(4);
        (await new SqlRestrictedPartyRepository(Factory).GetAllAsync()).ShouldContain(s => s.VendorId.Value == "VND-1678");
        (await new SqlCurrencyRateRepository(Factory).GetAllAsync()).ShouldContain(r => r.From.Value == "USD" && r.To.Value == "EUR");
    }

    [SqlServerFact]
    public async Task Scoring_OnSqlData_RanksInternationalBicyclesFirstAndProsewareBlocked()
    {
        Rfp rfp = (await new SqlRfpRepository(Factory).GetByIdAsync(RfpId.Create("RFP-2026-017").Value))!;
        IReadOnlyList<Bid> bids = await new SqlBidRepository(Factory).GetByRfpAsync(rfp.Id);
        IReadOnlyList<Vendor> vendors = await new SqlVendorRepository(Factory).GetAllAsync();
        var scoring = new BidScoringService(new Domain.Currency.CurrencyConverter(await new SqlCurrencyRateRepository(Factory).GetAllAsync()));

        Result<IReadOnlyList<BidScore>> scores = scoring.ScoreAll(rfp, bids, vendors.ToDictionary(v => v.Id));

        scores.IsSuccess.ShouldBeTrue(scores.Error.ToString());
        scores.Value[0].VendorId.Value.ShouldBe("VND-1526");
        scores.Value.Single(s => s.BidId.Value == "BID-003").UnitPriceInRfpCurrency.Currency.Value.ShouldBe("USD");
        scores.Value.Select(s => s.BidId.Value).ShouldBe(["BID-003", "BID-004", "BID-001", "BID-005", "BID-002"]);
        scores.Value[0].WeightedTotal.ShouldBe(94.60m);
        scores.Value.Single(s => s.BidId.Value == "BID-003").UnitPriceInRfpCurrency.Amount.ShouldBe(38.35m);
    }

    [SqlServerFact]
    public async Task QueryExecutor_ImpersonatesReaderWithSelectOnCopilotOnly()
    {
        var executor = new SqlReadOnlyQueryExecutor(Factory);

        Result<QueryResultSet> allowed = await executor.ExecuteAsync("SELECT TOP 3 VendorId, VendorName FROM copilot.Vendors ORDER BY VendorName", 10);
        Result<QueryResultSet> denied = await executor.ExecuteAsync("SELECT TOP 1 Name FROM Purchasing.Vendor", 10);
        Result<QueryResultSet> capped = await executor.ExecuteAsync("SELECT VendorId FROM copilot.Vendors", 5);

        allowed.IsSuccess.ShouldBeTrue();
        allowed.Value.Columns.ShouldBe(["VendorId", "VendorName"]);
        allowed.Value.Rows.Count.ShouldBe(3);
        denied.IsFailure.ShouldBeTrue();
        denied.Error.Message.ShouldContain("permission");
        capped.Value.Rows.Count.ShouldBe(5);
        capped.Value.Truncated.ShouldBeTrue();
    }

    [SqlServerFact]
    public async Task AwardRecorder_InsertsPendingPurchaseOrderAndRefusesRestrictedParty()
    {
        var recorder = new SqlAwardRecorder(Factory, new FileAwardRecorderStub());
        var request = new AwardRecordRequest(RfpId.Create("RFP-2026-017").Value, VendorId.Create("VND-1526").Value, "International Bicycles", 90m, "integration test", 38.35m, 2, DateTimeOffset.UtcNow);

        Result<AwardRecordReference> result = await recorder.RecordAsync(request);
        Result<AwardRecordReference> refused = await recorder.RecordAsync(request with { VendorId = VendorId.Create("VND-1678").Value, VendorName = "Proseware" });

        try
        {
            result.IsSuccess.ShouldBeTrue(result.Error.ToString());
            result.Value.PurchaseOrderId.ShouldNotBeNull();
            await using SqlConnection connection = Factory.Create();
            (await connection.QuerySingleAsync<int>("SELECT Status FROM Purchasing.PurchaseOrderHeader WHERE PurchaseOrderID = @id", new { id = result.Value.PurchaseOrderId })).ShouldBe(1);
            (await connection.QuerySingleAsync<int>("SELECT OrderQty FROM Purchasing.PurchaseOrderDetail WHERE PurchaseOrderID = @id", new { id = result.Value.PurchaseOrderId })).ShouldBe(2000);
            refused.Error.Code.ShouldBe("Award.VendorSanctioned");
        }
        finally
        {
            await CleanupAsync(result.IsSuccess ? result.Value.PurchaseOrderId : null);
        }
    }

    private static async Task CleanupAsync(int? purchaseOrderId)
    {
        if (purchaseOrderId is null)
        {
            return;
        }

        await using SqlConnection connection = Factory.Create();
        await connection.ExecuteAsync(
            "DELETE FROM Procurement.AwardRecommendation WHERE PurchaseOrderID = @id; DELETE FROM Purchasing.PurchaseOrderDetail WHERE PurchaseOrderID = @id; DELETE FROM Purchasing.PurchaseOrderHeader WHERE PurchaseOrderID = @id",
            new { id = purchaseOrderId });
    }

    private sealed class FileAwardRecorderStub : IAwardRecorder
    {
        public Task<Result<AwardRecordReference>> RecordAsync(AwardRecordRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AwardRecordReference>.Success(new AwardRecordReference("stub", null)));
    }
}
