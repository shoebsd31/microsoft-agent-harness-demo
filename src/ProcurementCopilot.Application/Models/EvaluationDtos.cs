namespace ProcurementCopilot.Application.Models;

/// <summary>Per-criterion scores returned by <c>score_bid</c>.</summary>
public sealed record CriterionBreakdown(decimal Price, decimal LeadTime, decimal Warranty, decimal Technical, decimal Sustainability);

/// <summary>Result of <c>score_bid</c>.</summary>
public sealed record ScoreReport(
    string RfpId,
    string BidId,
    string VendorId,
    decimal UnitPriceInRfpCurrency,
    string Currency,
    CriterionBreakdown Criteria,
    CriteriaWeights Weights,
    decimal WeightedTotal,
    int Rank,
    int BidsScored,
    int BidsTotal,
    string Source);

/// <summary>Result of <c>check_vendor_compliance</c>.</summary>
public sealed record ComplianceReport(
    string VendorId,
    string VendorName,
    bool Sanctioned,
    string? SanctionsList,
    string? SanctionsReason,
    IReadOnlyList<string> RequiredCertifications,
    IReadOnlyList<string> MissingCertifications,
    string Verdict,
    string Disposition,
    string Source);

/// <summary>Result of <c>draft_clarification_email</c>.</summary>
public sealed record ClarificationDraftReport(string DraftId, string VendorId, string Subject, string SavedTo, string Disposition);

/// <summary>Result of <c>record_award_recommendation</c>.</summary>
public sealed record AwardRecommendationReport(string RfpId, string VendorId, decimal? WinningScore, string SavedTo, string RecordedAt, int? PurchaseOrderId = null);

/// <summary>Outstanding work for the active RFP.</summary>
public sealed record EvaluationProgress(
    string? ActiveRfpId,
    int BidsScored,
    int BidsTotal,
    IReadOnlyList<string> UnscoredBidIds,
    IReadOnlyList<string> OpenComplianceVendorIds,
    bool IsComplete);

/// <summary>Result of <c>query_readonly</c>: a capped tabular result.</summary>
public sealed record QueryToolResult(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows, int RowCount, bool Truncated, long ElapsedMilliseconds, string Source);
