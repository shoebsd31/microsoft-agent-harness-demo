using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Currency;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Scoring;
using ProcurementCopilot.Domain.ValueObjects;
using Shouldly;

namespace ProcurementCopilot.Domain.Tests;

public class BidScoringServiceTests
{
    private static BidScoringService Sut() => new(new CurrencyConverter(SeedFixtures.Rates()));

    [Theory]
    [InlineData("BID-003", 89.60)]
    [InlineData("BID-004", 87.60)]
    [InlineData("BID-005", 85.24)]
    [InlineData("BID-001", 82.56)]
    [InlineData("BID-002", 68.84)]
    public void ScoreAll_SeededRfp_ProducesPinnedScores(string bidId, decimal expected)
    {
        Result<IReadOnlyList<BidScore>> result = Sut().ScoreAll(SeedFixtures.Rfp017(), SeedFixtures.Bids(), SeedFixtures.Vendors());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Single(s => s.BidId.Value == bidId).WeightedTotal.ShouldBe(expected);
    }

    [Fact]
    public void ScoreAll_SeededRfp_RanksApexFirstAndNordicLast()
    {
        IReadOnlyList<BidScore> scores = Sut().ScoreAll(SeedFixtures.Rfp017(), SeedFixtures.Bids(), SeedFixtures.Vendors()).Value;

        scores.Select(s => s.BidId.Value).ShouldBe(["BID-003", "BID-004", "BID-005", "BID-001", "BID-002"]);
    }

    [Fact]
    public void ScoreAll_UsdBid_IsConvertedToEur()
    {
        IReadOnlyList<BidScore> scores = Sut().ScoreAll(SeedFixtures.Rfp017(), SeedFixtures.Bids(), SeedFixtures.Vendors()).Value;

        BidScore apex = scores.Single(s => s.BidId.Value == "BID-003");
        apex.UnitPriceInRfpCurrency.Amount.ShouldBe(183080.00m);
        apex.UnitPriceInRfpCurrency.Currency.ShouldBe(CurrencyCode.Eur);
    }

    [Fact]
    public void ScoreAll_CriterionScores_AreNormalisedAgainstBestBid()
    {
        IReadOnlyList<BidScore> scores = Sut().ScoreAll(SeedFixtures.Rfp017(), SeedFixtures.Bids(), SeedFixtures.Vendors()).Value;

        BidScore cheapest = scores.Single(s => s.BidId.Value == "BID-004");
        cheapest.Criteria.Price.ShouldBe(100m);
        cheapest.Criteria.LeadTime.ShouldBe(100m);
        scores.Single(s => s.BidId.Value == "BID-003").Criteria.Warranty.ShouldBe(100m);
        scores.Single(s => s.BidId.Value == "BID-002").Criteria.Sustainability.ShouldBe(0m);
    }

    [Fact]
    public void ScoreAll_WeightsScaledByTen_ProducesSameTotals()
    {
        var scaled = new EvaluationCriteria(350, 200, 150, 200, 100);

        IReadOnlyList<BidScore> baseline = Sut().ScoreAll(SeedFixtures.Rfp017(), SeedFixtures.Bids(), SeedFixtures.Vendors()).Value;
        IReadOnlyList<BidScore> result = Sut().ScoreAll(SeedFixtures.Rfp017(), scaled, SeedFixtures.Bids(), SeedFixtures.Vendors()).Value;

        result.Select(s => s.WeightedTotal).ShouldBe(baseline.Select(s => s.WeightedTotal));
    }

    [Fact]
    public void ScoreAll_SustainabilityOverride_ChangesRanking()
    {
        EvaluationCriteria criteria = SeedFixtures.Rfp017().Criteria.WithSustainability(40);

        IReadOnlyList<BidScore> result = Sut().ScoreAll(SeedFixtures.Rfp017(), criteria, SeedFixtures.Bids(), SeedFixtures.Vendors()).Value;

        result.Single(s => s.BidId.Value == "BID-002").WeightedTotal.ShouldBeLessThan(60m);
        result[0].BidId.Value.ShouldBe("BID-003");
    }

    [Fact]
    public void ScoreAll_ZeroWeights_ReturnsInvalidWeights()
    {
        Result<IReadOnlyList<BidScore>> result = Sut().ScoreAll(SeedFixtures.Rfp017(), new EvaluationCriteria(0, 0, 0, 0, 0), SeedFixtures.Bids(), SeedFixtures.Vendors());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Scoring.InvalidWeights");
    }

    [Fact]
    public void ScoreAll_NoBids_ReturnsNoBids()
    {
        Sut().ScoreAll(SeedFixtures.Rfp017(), [], SeedFixtures.Vendors()).Error.Code.ShouldBe("Scoring.NoBids");
    }

    [Fact]
    public void ScoreAll_MissingRate_ReturnsRateNotFound()
    {
        var sut = new BidScoringService(new CurrencyConverter([]));

        sut.ScoreAll(SeedFixtures.Rfp017(), SeedFixtures.Bids(), SeedFixtures.Vendors()).Error.Code.ShouldBe("Fx.RateNotFound");
    }

    [Fact]
    public void ScoreAll_BidWithZeroLeadTime_ReturnsMissingData()
    {
        List<Bid> bids = SeedFixtures.Bids();
        bids[0] = bids[0] with { LeadTimeWeeks = 0 };

        Sut().ScoreAll(SeedFixtures.Rfp017(), bids, SeedFixtures.Vendors()).Error.Code.ShouldBe("Scoring.MissingData");
    }

    [Fact]
    public void Score_SingleBid_MatchesScoreAllEntry()
    {
        Bid bid = SeedFixtures.Bids()[2];

        Result<BidScore> single = Sut().Score(SeedFixtures.Rfp017(), bid, SeedFixtures.Bids(), SeedFixtures.Vendors());

        single.Value.WeightedTotal.ShouldBe(89.60m);
    }
}
