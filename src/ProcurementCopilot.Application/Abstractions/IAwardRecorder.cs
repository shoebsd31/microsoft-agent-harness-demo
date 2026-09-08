using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Application.Abstractions;

/// <summary>Everything needed to record an award recommendation, already validated by <c>AwardService</c>.</summary>
/// <param name="RfpId">The RFP.</param>
/// <param name="VendorId">The recommended vendor.</param>
/// <param name="VendorName">Vendor display name.</param>
/// <param name="WinningScore">The persisted weighted score, when one exists.</param>
/// <param name="Rationale">Analyst-facing rationale.</param>
/// <param name="UnitPriceInRfpCurrency">The winning bid's unit price converted to the RFP currency.</param>
/// <param name="LeadTimeWeeks">The winning bid's lead time, used for the purchase-order due date.</param>
/// <param name="RecordedAt">Timestamp.</param>
public sealed record AwardRecordRequest(
    RfpId RfpId,
    VendorId VendorId,
    string VendorName,
    decimal? WinningScore,
    string Rationale,
    decimal UnitPriceInRfpCurrency,
    int LeadTimeWeeks,
    DateTimeOffset RecordedAt);

/// <summary>Where the recommendation was recorded.</summary>
/// <param name="Reference">Human-readable reference: a workspace path or a purchase-order number.</param>
/// <param name="PurchaseOrderId">The AdventureWorks purchase order created, when the SQL backend is used.</param>
public sealed record AwardRecordReference(string Reference, int? PurchaseOrderId);

/// <summary>Persists an award recommendation: a JSON document in the workspace, or a pending purchase order in AdventureWorks.</summary>
public interface IAwardRecorder
{
    /// <summary>Records the recommendation and returns where it went.</summary>
    Task<Result<AwardRecordReference>> RecordAsync(AwardRecordRequest request, CancellationToken cancellationToken = default);
}
