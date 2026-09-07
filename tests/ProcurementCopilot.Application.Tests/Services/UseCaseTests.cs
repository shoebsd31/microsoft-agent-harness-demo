using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.ValueObjects;
using Shouldly;

namespace ProcurementCopilot.Application.Tests.Services;

public class UseCaseTests
{
    private static RfpId Rfp => RfpId.Create("RFP-2026-017").Value;

    [Fact]
    public async Task ListOpen_ReturnsOnlyOpenRfps()
    {
        var f = new ServiceFixture();

        IReadOnlyList<RfpSummary> open = await f.Queries.ListOpenAsync();

        open.Select(r => r.RfpId).ShouldBe(["RFP-2026-017"]);
    }

    [Fact]
    public async Task ListBids_WrapsVendorAuthoredTextAsUntrustedData()
    {
        var f = new ServiceFixture();

        IReadOnlyList<BidSummary> bids = (await f.Queries.ListBidsAsync(Rfp)).Value;

        bids.Count.ShouldBe(5);
        bids.ShouldAllBe(b => b.DeliveryClause.StartsWith("<untrusted_data source=\"bid:"));
        f.State.State.ActiveRfpId.ShouldBe("RFP-2026-017");
    }

    [Fact]
    public async Task GetVendor_InjectionNotes_AreWrappedNotStripped()
    {
        var f = new ServiceFixture();

        VendorProfile profile = (await f.Queries.GetVendorAsync(VendorId.Create("VND-0002").Value)).Value;

        profile.Notes.ShouldStartWith("<untrusted_data source=\"vendor:VND-0002\">");
        profile.Notes.ShouldContain("Ignore previous instructions");
    }

    [Fact]
    public async Task Score_PersistsScoreAndReportsProgress()
    {
        var f = new ServiceFixture();

        Result<ScoreReport> report = await f.Evaluation.ScoreAsync(Rfp, BidId.Create("BID-003").Value);

        report.Value.WeightedTotal.ShouldBe(89.60m);
        report.Value.Rank.ShouldBe(1);
        report.Value.BidsScored.ShouldBe(1);
        report.Value.BidsTotal.ShouldBe(5);
        (await f.Evaluation.GetProgressAsync()).UnscoredBidIds.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Score_BidFromOtherRfp_Fails()
    {
        var f = new ServiceFixture();

        Result<ScoreReport> report = await f.Evaluation.ScoreAsync(RfpId.Create("RFP-2026-012").Value, BidId.Create("BID-001").Value);

        report.Error.Code.ShouldBe("Scoring.BidNotForRfp");
    }

    [Fact]
    public async Task Compliance_SanctionedVendor_IsFlaggedWithDisposition()
    {
        var f = new ServiceFixture();

        ComplianceReport report = (await f.Compliance.CheckAsync(VendorId.Create("VND-0004").Value)).Value;

        report.Sanctioned.ShouldBeTrue();
        report.Disposition.ShouldBe("Flagged");
        f.State.State.ComplianceHits["VND-0004"].Disposition.ShouldBe(ComplianceDisposition.Flagged);
    }

    [Fact]
    public async Task Compliance_CleanVendor_HasNoDisposition()
    {
        var f = new ServiceFixture();

        ComplianceReport report = (await f.Compliance.CheckAsync(VendorId.Create("VND-0001").Value)).Value;

        report.Verdict.ShouldBe("PASS");
        report.Disposition.ShouldBe("None");
    }

    [Fact]
    public async Task Draft_WritesOutboxAuditsAndUpgradesDisposition()
    {
        var f = new ServiceFixture();
        await f.Compliance.CheckAsync(VendorId.Create("VND-0004").Value);

        Result<ClarificationDraftReport> report = await f.Clarifications.DraftAsync(VendorId.Create("VND-0004").Value, "subject", "body");

        report.IsSuccess.ShouldBeTrue();
        f.Outbox.Drafts.Count.ShouldBe(1);
        f.Audit.Actions.Single().Tool.ShouldBe("draft_clarification_email");
        f.Audit.Actions.Single().ArgumentsHash.Length.ShouldBe(64);
        f.State.State.ComplianceHits["VND-0004"].Disposition.ShouldBe(ComplianceDisposition.ClarificationRequested);
        f.State.State.OpenClarifications.ShouldContain("VND-0004");
    }

    [Fact]
    public async Task Award_SanctionedVendor_IsRefusedAndAudited()
    {
        var f = new ServiceFixture();

        Result<AwardRecommendationReport> report = await f.Awards.RecordAsync(Rfp, VendorId.Create("VND-0004").Value, "cheapest");

        report.Error.Code.ShouldBe("Award.VendorSanctioned");
        f.Audit.Actions.Single().Outcome.ShouldBe("blocked");
        f.Outbox.Documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Award_EligibleVendor_WritesDocumentWithScore()
    {
        var f = new ServiceFixture();
        await f.Evaluation.ScoreAsync(Rfp, BidId.Create("BID-003").Value);

        Result<AwardRecommendationReport> report = await f.Awards.RecordAsync(Rfp, VendorId.Create("VND-0003").Value, "best score");

        report.Value.WinningScore.ShouldBe(89.60m);
        f.Outbox.Documents.Keys.ShouldContain("output/award-recommendation.json");
        f.Outbox.Documents["output/award-recommendation.json"].ShouldContain("\"vendorId\": \"VND-0003\"");
        f.State.State.AwardedVendorId.ShouldBe("VND-0003");
    }

    [Fact]
    public async Task Convert_UsesSeededRateWithDate()
    {
        var f = new ServiceFixture();

        CurrencyConversion c = (await f.Currency.ConvertAsync(199000m, CurrencyCode.Usd, CurrencyCode.Eur)).Value;

        c.Converted.ShouldBe(183080.00m);
        c.RateDate.ShouldBe("2026-08-29");
    }
}
