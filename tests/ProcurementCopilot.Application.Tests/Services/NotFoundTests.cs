using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Domain.ValueObjects;
using Shouldly;

namespace ProcurementCopilot.Application.Tests.Services;

public class NotFoundTests
{
    private static readonly RfpId MissingRfp = RfpId.Create("RFP-2099-999").Value;
    private static readonly VendorId MissingVendor = VendorId.Create("VND-9999").Value;
    private static readonly BidId MissingBid = BidId.Create("BID-999").Value;

    [Fact]
    public async Task Queries_UnknownIds_ReturnNotFoundErrors()
    {
        var f = new ServiceFixture();

        (await f.Queries.GetAsync(MissingRfp)).Error.Code.ShouldBe("Rfp.NotFound");
        (await f.Queries.ListBidsAsync(MissingRfp)).Error.Code.ShouldBe("Rfp.NotFound");
        (await f.Queries.GetVendorAsync(MissingVendor)).Error.Code.ShouldBe("Vendor.NotFound");
    }

    [Fact]
    public async Task Evaluation_UnknownIds_ReturnNotFoundErrors()
    {
        var f = new ServiceFixture();

        (await f.Evaluation.ScoreAsync(MissingRfp, BidId.Create("BID-001").Value)).Error.Code.ShouldBe("Rfp.NotFound");
        (await f.Evaluation.ScoreAsync(RfpId.Create("RFP-2026-017").Value, MissingBid)).Error.Code.ShouldBe("Bid.NotFound");
    }

    [Fact]
    public async Task Progress_NoActiveRfp_IsEmptyAndIncomplete()
    {
        var f = new ServiceFixture();

        EvaluationProgress progress = await f.Evaluation.GetProgressAsync();

        progress.ActiveRfpId.ShouldBeNull();
        progress.IsComplete.ShouldBeFalse();
        progress.BidsTotal.ShouldBe(0);
    }

    [Fact]
    public async Task Progress_AllScoredAndFlagged_IsComplete()
    {
        var f = new ServiceFixture();
        foreach (string bid in new[] { "BID-001", "BID-002", "BID-003", "BID-004", "BID-005" })
        {
            await f.Evaluation.ScoreAsync(RfpId.Create("RFP-2026-017").Value, BidId.Create(bid).Value);
        }

        await f.Compliance.CheckAsync(VendorId.Create("VND-0004").Value);
        EvaluationProgress progress = await f.Evaluation.GetProgressAsync();

        progress.IsComplete.ShouldBeTrue();
        progress.BidsScored.ShouldBe(5);
        progress.OpenComplianceVendorIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Compliance_UnknownVendor_ReturnsNotFound()
    {
        var f = new ServiceFixture();

        (await f.Compliance.CheckAsync(MissingVendor)).Error.Code.ShouldBe("Vendor.NotFound");
    }

    [Fact]
    public async Task Compliance_NoActiveRfp_UsesFirstOpenRfpAndRecordsHitOnce()
    {
        var f = new ServiceFixture();
        f.State.State.ActiveRfpId = null;

        ComplianceReport first = (await f.Compliance.CheckAsync(VendorId.Create("VND-0004").Value)).Value;
        ComplianceReport second = (await f.Compliance.CheckAsync(VendorId.Create("VND-0004").Value)).Value;

        first.RequiredCertifications.ShouldBe(["ISO 9001", "CE"]);
        second.Disposition.ShouldBe("Flagged");
        f.State.State.ComplianceHits.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Compliance_ActiveRfpUnknown_FallsBackToOpenRfp()
    {
        var f = new ServiceFixture();
        f.State.State.ActiveRfpId = "RFP-2099-999";

        (await f.Compliance.CheckAsync(VendorId.Create("VND-0001").Value)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SideEffects_UnknownIds_ReturnNotFoundErrors()
    {
        var f = new ServiceFixture();

        (await f.Clarifications.DraftAsync(MissingVendor, "s", "b")).Error.Code.ShouldBe("Vendor.NotFound");
        (await f.Awards.RecordAsync(MissingRfp, VendorId.Create("VND-0001").Value, "r")).Error.Code.ShouldBe("Rfp.NotFound");
        (await f.Awards.RecordAsync(RfpId.Create("RFP-2026-017").Value, MissingVendor, "r")).Error.Code.ShouldBe("Vendor.NotFound");
        (await f.Currency.ConvertAsync(1m, CurrencyCode.Create("GBP").Value, CurrencyCode.Create("JPY").Value)).Error.Code.ShouldBe("Fx.RateNotFound");
        f.Outbox.Drafts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Draft_WithoutPriorComplianceHit_StillRecordsOpenClarification()
    {
        var f = new ServiceFixture();

        ClarificationDraftReport report = (await f.Clarifications.DraftAsync(VendorId.Create("VND-0005").Value, "s", "b")).Value;

        report.Disposition.ShouldContain("no compliance hit");
        f.State.State.OpenClarifications.ShouldBe(["VND-0005"]);
        f.State.State.ComplianceHits.ShouldBeEmpty();
    }
}
