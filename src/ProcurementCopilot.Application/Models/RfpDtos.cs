namespace ProcurementCopilot.Application.Models;

/// <summary>An RFP in list output.</summary>
public sealed record RfpSummary(string RfpId, string Title, string Status, string Currency, int Quantity, string Category);

/// <summary>Weights returned to the model.</summary>
public sealed record CriteriaWeights(decimal Price, decimal LeadTime, decimal Warranty, decimal Technical, decimal Sustainability);

/// <summary>Full RFP detail returned by <c>get_rfp</c>.</summary>
public sealed record RfpDetail(
    string RfpId,
    string Title,
    string Description,
    string Status,
    string Currency,
    int Quantity,
    string Category,
    CriteriaWeights Weights,
    IReadOnlyList<string> RequiredCertifications,
    int BidCount);

/// <summary>A bid as returned by <c>list_bids</c>. Vendor-authored text is wrapped in the untrusted-data envelope.</summary>
public sealed record BidSummary(
    string BidId,
    string RfpId,
    string VendorId,
    string VendorName,
    decimal UnitPrice,
    string Currency,
    int LeadTimeWeeks,
    int WarrantyMonths,
    decimal TechnicalCompliancePercent,
    string DeliveryClause,
    string Notes);

/// <summary>Vendor profile returned by <c>get_vendor_profile</c>. Vendor notes are wrapped in the untrusted-data envelope.</summary>
public sealed record VendorProfile(
    string VendorId,
    string Name,
    string Country,
    IReadOnlyList<string> Certifications,
    int YearsTrading,
    string Notes);

/// <summary>Result of <c>convert_currency</c>.</summary>
public sealed record CurrencyConversion(decimal Amount, string From, decimal Converted, string To, decimal Rate, string RateDate, string Source);
