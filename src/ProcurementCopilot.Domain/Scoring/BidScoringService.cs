using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Currency;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Errors;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Scoring;

/// <summary>
/// Pure, deterministic weighted scoring of bids against an RFP's criteria.
/// Relative criteria (price, lead time, warranty) are normalised against the best bid in the RFP.
/// </summary>
public sealed class BidScoringService
{
    /// <summary>Certification that earns the sustainability points.</summary>
    public const string SustainabilityCertification = "ISO 14001";

    private readonly CurrencyConverter _converter;

    /// <summary>Initializes the service with a converter for foreign-currency bids.</summary>
    public BidScoringService(CurrencyConverter converter) => _converter = converter;

    /// <summary>Scores every bid of the RFP. Fails if any bid cannot be converted or is missing data.</summary>
    public Result<IReadOnlyList<BidScore>> ScoreAll(Rfp rfp, IReadOnlyList<Bid> bids, IReadOnlyDictionary<VendorId, Vendor> vendors) =>
        ScoreAll(rfp, rfp.Criteria, bids, vendors);

    /// <summary>Scores every bid of the RFP using explicit criteria weights (for analyst overrides).</summary>
    public Result<IReadOnlyList<BidScore>> ScoreAll(Rfp rfp, EvaluationCriteria criteria, IReadOnlyList<Bid> bids, IReadOnlyDictionary<VendorId, Vendor> vendors)
    {
        Result<EvaluationCriteria> weights = criteria.Validate();
        if (weights.IsFailure)
        {
            return weights.Error;
        }

        if (bids.Count == 0)
        {
            return DomainErrors.Scoring.NoBids;
        }

        if (bids.Any(b => b.RfpId != rfp.Id))
        {
            return DomainErrors.Scoring.BidNotForRfp;
        }

        if (bids.Any(b => !b.HasScoringData))
        {
            return DomainErrors.Scoring.MissingData;
        }

        var prices = new Dictionary<BidId, Money>();
        foreach (Bid bid in bids)
        {
            Result<Money> converted = _converter.Convert(bid.UnitPrice, rfp.Currency);
            if (converted.IsFailure)
            {
                return converted.Error;
            }

            prices[bid.Id] = converted.Value;
        }

        decimal minPrice = prices.Values.Min(p => p.Amount);
        int minLead = bids.Min(b => b.LeadTimeWeeks);
        int maxWarranty = bids.Max(b => b.WarrantyMonths);

        var scores = new List<BidScore>(bids.Count);
        foreach (Bid bid in bids)
        {
            bool sustainable = vendors.TryGetValue(bid.VendorId, out Vendor? vendor) && vendor.Holds(SustainabilityCertification);
            var criterion = new CriterionScores(
                Price: Ratio(minPrice, prices[bid.Id].Amount),
                LeadTime: Ratio(minLead, bid.LeadTimeWeeks),
                Warranty: maxWarranty == 0 ? 100m : Ratio(bid.WarrantyMonths, maxWarranty),
                Technical: decimal.Round(bid.TechnicalCompliancePercent, 2),
                Sustainability: sustainable ? 100m : 0m);

            scores.Add(new BidScore(bid.Id, bid.VendorId, prices[bid.Id], criterion, Weighted(criterion, weights.Value)));
        }

        return scores.OrderByDescending(s => s.WeightedTotal).ThenBy(s => s.BidId.Value, StringComparer.Ordinal).ToList();
    }

    /// <summary>Scores a single bid in the context of all bids for the RFP (relative criteria need the peers).</summary>
    public Result<BidScore> Score(Rfp rfp, Bid bid, IReadOnlyList<Bid> allBids, IReadOnlyDictionary<VendorId, Vendor> vendors) =>
        Score(rfp, rfp.Criteria, bid, allBids, vendors);

    /// <summary>Scores a single bid with explicit criteria weights.</summary>
    public Result<BidScore> Score(Rfp rfp, EvaluationCriteria criteria, Bid bid, IReadOnlyList<Bid> allBids, IReadOnlyDictionary<VendorId, Vendor> vendors) =>
        ScoreAll(rfp, criteria, allBids, vendors).Bind(all =>
            all.FirstOrDefault(s => s.BidId == bid.Id) is { } score
                ? Result<BidScore>.Success(score)
                : DomainErrors.NotFound.Bid);

    private static decimal Ratio(decimal numerator, decimal denominator) =>
        denominator <= 0 ? 0m : decimal.Round(100m * numerator / denominator, 2, MidpointRounding.AwayFromZero);

    private static decimal Weighted(CriterionScores s, EvaluationCriteria w)
    {
        decimal total = w.Total;
        decimal sum = (s.Price * w.Price) + (s.LeadTime * w.LeadTime) + (s.Warranty * w.Warranty) +
                      (s.Technical * w.Technical) + (s.Sustainability * w.Sustainability);
        return decimal.Round(sum / total, 2, MidpointRounding.AwayFromZero);
    }
}
