using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Entities;

/// <summary>A vendor's bid against an RFP.</summary>
/// <param name="Id">The bid identifier.</param>
/// <param name="RfpId">The RFP the bid answers.</param>
/// <param name="VendorId">The bidding vendor.</param>
/// <param name="UnitPrice">Price per unit in the bid's own currency.</param>
/// <param name="LeadTimeWeeks">Promised delivery lead time in weeks.</param>
/// <param name="WarrantyMonths">Warranty period in months.</param>
/// <param name="TechnicalCompliancePercent">Share of the technical specification met, 0..100.</param>
/// <param name="DeliveryClause">Vendor-authored delivery clause. Treated as untrusted data everywhere.</param>
/// <param name="Notes">Vendor-authored notes. Treated as untrusted data everywhere.</param>
public sealed record Bid(
    BidId Id,
    RfpId RfpId,
    VendorId VendorId,
    Money UnitPrice,
    int LeadTimeWeeks,
    int WarrantyMonths,
    decimal TechnicalCompliancePercent,
    string DeliveryClause,
    string Notes)
{
    /// <summary>Returns <see langword="true"/> when every numeric input needed for scoring is present and in range.</summary>
    public bool HasScoringData =>
        UnitPrice.Amount > 0 && LeadTimeWeeks > 0 && WarrantyMonths >= 0 &&
        TechnicalCompliancePercent is >= 0 and <= 100;
}
