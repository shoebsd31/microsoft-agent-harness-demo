using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcurementCopilot.Infrastructure.Data;

/// <summary>Raw JSON shape of an RFP in <c>data/rfps.json</c>.</summary>
public sealed class RfpRecord
{
    /// <summary>Identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Quantity.</summary>
    public int Quantity { get; set; }

    /// <summary>Currency code.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Open or Closed.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Category.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Weights.</summary>
    public CriteriaRecord Criteria { get; set; } = new();

    /// <summary>Required certifications.</summary>
    public List<string> RequiredCertifications { get; set; } = [];
}

/// <summary>Raw JSON shape of criteria weights.</summary>
public sealed class CriteriaRecord
{
    /// <summary>Price weight.</summary>
    public decimal Price { get; set; }

    /// <summary>Lead-time weight.</summary>
    public decimal LeadTime { get; set; }

    /// <summary>Warranty weight.</summary>
    public decimal Warranty { get; set; }

    /// <summary>Technical weight.</summary>
    public decimal Technical { get; set; }

    /// <summary>Sustainability weight.</summary>
    public decimal Sustainability { get; set; }
}

/// <summary>Raw JSON shape of a vendor in <c>data/vendors.json</c>.</summary>
public sealed class VendorRecord
{
    /// <summary>Identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Country.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Certifications.</summary>
    public List<string> Certifications { get; set; } = [];

    /// <summary>Years trading.</summary>
    public int YearsTrading { get; set; }

    /// <summary>Vendor-authored notes (untrusted).</summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>Raw JSON shape of a bid in <c>data/bids.json</c>.</summary>
public sealed class BidRecord
{
    /// <summary>Identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>RFP id.</summary>
    public string RfpId { get; set; } = string.Empty;

    /// <summary>Vendor id.</summary>
    public string VendorId { get; set; } = string.Empty;

    /// <summary>Unit price.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Currency code.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Lead time in weeks.</summary>
    public int LeadTimeWeeks { get; set; }

    /// <summary>Warranty in months.</summary>
    public int WarrantyMonths { get; set; }

    /// <summary>Technical compliance percentage.</summary>
    public decimal TechnicalCompliancePercent { get; set; }

    /// <summary>Vendor-authored delivery clause (untrusted).</summary>
    public string DeliveryClause { get; set; } = string.Empty;

    /// <summary>Vendor-authored notes (untrusted).</summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>Raw JSON shape of <c>data/fx-rates.json</c>.</summary>
public sealed class FxRatesFile
{
    /// <summary>Date the rates were fixed (yyyy-MM-dd).</summary>
    public string AsOf { get; set; } = string.Empty;

    /// <summary>Rates.</summary>
    public List<FxRateRecord> Rates { get; set; } = [];
}

/// <summary>Raw JSON shape of one exchange rate.</summary>
public sealed class FxRateRecord
{
    /// <summary>Source currency.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Target currency.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>Multiplier.</summary>
    public decimal Rate { get; set; }
}

/// <summary>Source-generated JSON context for the seed files.</summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(List<RfpRecord>))]
[JsonSerializable(typeof(List<VendorRecord>))]
[JsonSerializable(typeof(List<BidRecord>))]
[JsonSerializable(typeof(FxRatesFile))]
public sealed partial class SeedJsonContext : JsonSerializerContext;
