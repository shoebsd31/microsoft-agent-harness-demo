using System.Diagnostics.CodeAnalysis;

namespace ProcurementCopilot.Infrastructure.SqlServer;

/// <summary>Row shape of <c>copilot.Rfps</c>.</summary>
[ExcludeFromCodeCoverage]
public sealed class RfpRow
{
    /// <summary>RFP id.</summary>
    public string RfpId { get; set; } = string.Empty;

    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Product id.</summary>
    public int ProductID { get; set; }

    /// <summary>Product name.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Quantity.</summary>
    public int Quantity { get; set; }

    /// <summary>Currency code.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Category.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Weights.</summary>
    public decimal WeightPrice { get; set; }

    /// <summary>Weights.</summary>
    public decimal WeightLeadTime { get; set; }

    /// <summary>Weights.</summary>
    public decimal WeightWarranty { get; set; }

    /// <summary>Weights.</summary>
    public decimal WeightTechnical { get; set; }

    /// <summary>Weights.</summary>
    public decimal WeightSustainability { get; set; }

    /// <summary>Comma-separated required certifications.</summary>
    public string? RequiredCertifications { get; set; }
}

/// <summary>Row shape of <c>copilot.Vendors</c>.</summary>
[ExcludeFromCodeCoverage]
public sealed class VendorRow
{
    /// <summary>BusinessEntityID.</summary>
    public int BusinessEntityID { get; set; }

    /// <summary>Name.</summary>
    public string VendorName { get; set; } = string.Empty;

    /// <summary>Credit rating.</summary>
    public int CreditRating { get; set; }

    /// <summary>Preferred flag.</summary>
    public bool PreferredVendorStatus { get; set; }

    /// <summary>Active flag.</summary>
    public bool ActiveFlag { get; set; }

    /// <summary>Country.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Years trading.</summary>
    public int? YearsTrading { get; set; }

    /// <summary>Notes (untrusted).</summary>
    public string? Notes { get; set; }

    /// <summary>Comma-separated certifications.</summary>
    public string? Certifications { get; set; }

    /// <summary>Contact email.</summary>
    public string? ContactEmail { get; set; }
}

/// <summary>Row shape of <c>copilot.Bids</c>.</summary>
[ExcludeFromCodeCoverage]
public sealed class BidRow
{
    /// <summary>Bid id.</summary>
    public string BidId { get; set; } = string.Empty;

    /// <summary>RFP id.</summary>
    public string RfpId { get; set; } = string.Empty;

    /// <summary>Vendor BusinessEntityID.</summary>
    public int BusinessEntityID { get; set; }

    /// <summary>Unit price.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Currency code.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Lead time in weeks.</summary>
    public int LeadTimeWeeks { get; set; }

    /// <summary>Warranty in months.</summary>
    public int WarrantyMonths { get; set; }

    /// <summary>Technical compliance percent.</summary>
    public decimal TechnicalCompliancePercent { get; set; }

    /// <summary>Delivery clause (untrusted).</summary>
    public string DeliveryClause { get; set; } = string.Empty;

    /// <summary>Notes (untrusted).</summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>Row shape of <c>copilot.RestrictedParties</c>.</summary>
[ExcludeFromCodeCoverage]
public sealed class RestrictedPartyRow
{
    /// <summary>Vendor BusinessEntityID.</summary>
    public int BusinessEntityID { get; set; }

    /// <summary>List name.</summary>
    public string ListName { get; set; } = string.Empty;

    /// <summary>Listed on.</summary>
    public DateTime ListedOn { get; set; }

    /// <summary>Reason.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Row shape of <c>copilot.CurrencyRates</c>.</summary>
[ExcludeFromCodeCoverage]
public sealed class CurrencyRateRow
{
    /// <summary>From currency.</summary>
    public string FromCurrencyCode { get; set; } = string.Empty;

    /// <summary>To currency.</summary>
    public string ToCurrencyCode { get; set; } = string.Empty;

    /// <summary>Rate.</summary>
    public decimal Rate { get; set; }

    /// <summary>Rate date.</summary>
    public DateTime AsOf { get; set; }
}
