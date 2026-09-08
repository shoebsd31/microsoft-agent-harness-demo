namespace ProcurementCopilot.Application.Abstractions;

/// <summary>An RFP as edited in the admin app.</summary>
public sealed class AdminRfp
{
    /// <summary>Id such as <c>RFP-2026-017</c>.</summary>
    public string RfpId { get; set; } = string.Empty;

    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>AdventureWorks product id.</summary>
    public int ProductId { get; set; }

    /// <summary>Product name (read-only).</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Quantity.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Currency code.</summary>
    public string CurrencyCode { get; set; } = "USD";

    /// <summary>Open or Closed.</summary>
    public string Status { get; set; } = "Open";

    /// <summary>Category.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Weights.</summary>
    public decimal WeightPrice { get; set; } = 35;

    /// <summary>Weights.</summary>
    public decimal WeightLeadTime { get; set; } = 20;

    /// <summary>Weights.</summary>
    public decimal WeightWarranty { get; set; } = 15;

    /// <summary>Weights.</summary>
    public decimal WeightTechnical { get; set; } = 20;

    /// <summary>Weights.</summary>
    public decimal WeightSustainability { get; set; } = 10;

    /// <summary>Required certifications, comma separated.</summary>
    public string RequiredCertifications { get; set; } = string.Empty;

    /// <summary>Number of bids (read-only).</summary>
    public int BidCount { get; set; }
}

/// <summary>A bid as edited in the admin app.</summary>
public sealed class AdminBid
{
    /// <summary>Identity (0 for new).</summary>
    public int BidNumber { get; set; }

    /// <summary>Computed id (read-only).</summary>
    public string BidId { get; set; } = string.Empty;

    /// <summary>RFP id.</summary>
    public string RfpId { get; set; } = string.Empty;

    /// <summary>Vendor BusinessEntityID.</summary>
    public int BusinessEntityId { get; set; }

    /// <summary>Vendor name (read-only).</summary>
    public string VendorName { get; set; } = string.Empty;

    /// <summary>Unit price.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Currency.</summary>
    public string CurrencyCode { get; set; } = "USD";

    /// <summary>Lead time in weeks.</summary>
    public int LeadTimeWeeks { get; set; } = 4;

    /// <summary>Warranty in months.</summary>
    public int WarrantyMonths { get; set; } = 12;

    /// <summary>Technical compliance percent.</summary>
    public decimal TechnicalCompliancePercent { get; set; } = 100;

    /// <summary>Delivery clause.</summary>
    public string DeliveryClause { get; set; } = string.Empty;

    /// <summary>Notes.</summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>An AdventureWorks vendor with its copilot profile.</summary>
public sealed class AdminVendor
{
    /// <summary>BusinessEntityID.</summary>
    public int BusinessEntityId { get; set; }

    /// <summary><c>VND-nnnn</c>.</summary>
    public string VendorId { get; set; } = string.Empty;

    /// <summary>Name (read-only, from AdventureWorks).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Credit rating (read-only).</summary>
    public int CreditRating { get; set; }

    /// <summary>Active flag (read-only).</summary>
    public bool Active { get; set; }

    /// <summary>Country from the address (read-only).</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Country override.</summary>
    public string? CountryOverride { get; set; }

    /// <summary>Years trading.</summary>
    public int? YearsTrading { get; set; }

    /// <summary>Vendor-authored notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Certifications, comma separated.</summary>
    public string Certifications { get; set; } = string.Empty;

    /// <summary>Whether the vendor is on the restricted list (read-only).</summary>
    public bool IsRestricted { get; set; }
}

/// <summary>A restricted-party entry.</summary>
public sealed class AdminRestrictedParty
{
    /// <summary>BusinessEntityID.</summary>
    public int BusinessEntityId { get; set; }

    /// <summary>Vendor name (read-only).</summary>
    public string VendorName { get; set; } = string.Empty;

    /// <summary>List name.</summary>
    public string ListName { get; set; } = "Adventure Works Restricted Parties List";

    /// <summary>Listed on.</summary>
    public DateTime ListedOn { get; set; } = DateTime.Today;

    /// <summary>Reason.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>An award recommendation with its purchase order.</summary>
public sealed record AdminAward(int AwardRecommendationId, string RfpId, string VendorId, string VendorName, int? PurchaseOrderId, decimal? WinningScore, string Rationale, DateTime RecordedAt, string RecordedBy, string? PurchaseOrderStatus, decimal? TotalDue);

/// <summary>A product pick-list entry.</summary>
public sealed record ProductPick(int ProductId, string Name, string ProductNumber, string? Subcategory);

/// <summary>CRUD over the Procurement schema for the admin app. AdventureWorks base tables are read-only here.</summary>
public interface IProcurementAdminStore
{
    /// <summary>Lists RFPs.</summary>
    Task<IReadOnlyList<AdminRfp>> ListRfpsAsync(CancellationToken ct = default);

    /// <summary>Gets one RFP.</summary>
    Task<AdminRfp?> GetRfpAsync(string rfpId, CancellationToken ct = default);

    /// <summary>Inserts or updates an RFP with its required certifications.</summary>
    Task SaveRfpAsync(AdminRfp rfp, CancellationToken ct = default);

    /// <summary>Deletes an RFP and its bids.</summary>
    Task DeleteRfpAsync(string rfpId, CancellationToken ct = default);

    /// <summary>Lists bids, optionally for one RFP.</summary>
    Task<IReadOnlyList<AdminBid>> ListBidsAsync(string? rfpId, CancellationToken ct = default);

    /// <summary>Inserts (BidNumber 0) or updates a bid.</summary>
    Task SaveBidAsync(AdminBid bid, CancellationToken ct = default);

    /// <summary>Deletes a bid.</summary>
    Task DeleteBidAsync(int bidNumber, CancellationToken ct = default);

    /// <summary>Lists all vendors with their profiles.</summary>
    Task<IReadOnlyList<AdminVendor>> ListVendorsAsync(CancellationToken ct = default);

    /// <summary>Saves a vendor's profile and certifications.</summary>
    Task SaveVendorProfileAsync(AdminVendor vendor, CancellationToken ct = default);

    /// <summary>Lists restricted parties.</summary>
    Task<IReadOnlyList<AdminRestrictedParty>> ListRestrictedPartiesAsync(CancellationToken ct = default);

    /// <summary>Adds or updates a restricted-party entry.</summary>
    Task SaveRestrictedPartyAsync(AdminRestrictedParty entry, CancellationToken ct = default);

    /// <summary>Removes a restricted-party entry.</summary>
    Task DeleteRestrictedPartyAsync(int businessEntityId, CancellationToken ct = default);

    /// <summary>Lists award recommendations, newest first.</summary>
    Task<IReadOnlyList<AdminAward>> ListAwardsAsync(CancellationToken ct = default);

    /// <summary>Searches products by name or number.</summary>
    Task<IReadOnlyList<ProductPick>> SearchProductsAsync(string term, CancellationToken ct = default);
}
