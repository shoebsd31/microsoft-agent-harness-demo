using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Scoring;

/// <summary>The scoring outcome for one bid.</summary>
/// <param name="BidId">The scored bid.</param>
/// <param name="VendorId">The bidding vendor.</param>
/// <param name="UnitPriceInRfpCurrency">Unit price converted into the RFP currency.</param>
/// <param name="Criteria">Per-criterion normalised scores.</param>
/// <param name="WeightedTotal">Weighted total, 0..100, rounded to two decimals.</param>
public sealed record BidScore(
    BidId BidId,
    VendorId VendorId,
    Money UnitPriceInRfpCurrency,
    CriterionScores Criteria,
    decimal WeightedTotal);
