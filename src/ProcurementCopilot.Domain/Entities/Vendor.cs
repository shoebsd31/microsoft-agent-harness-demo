using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Entities;

/// <summary>A supplier that may submit bids.</summary>
/// <param name="Id">The vendor identifier.</param>
/// <param name="Name">Legal name.</param>
/// <param name="Country">Country of registration.</param>
/// <param name="Certifications">Certifications the vendor claims to hold (for example <c>ISO 9001</c>).</param>
/// <param name="YearsTrading">Years in business.</param>
/// <param name="Notes">Free text supplied by the vendor. Treated as untrusted data everywhere.</param>
public sealed record Vendor(
    VendorId Id,
    string Name,
    string Country,
    IReadOnlyList<string> Certifications,
    int YearsTrading,
    string Notes)
{
    /// <summary>AdventureWorks credit rating 1 (excellent) to 5 (poor), when known.</summary>
    public int? CreditRating { get; init; }

    /// <summary>AdventureWorks preferred-vendor flag, when known.</summary>
    public bool? IsPreferred { get; init; }

    /// <summary>Whether the vendor is active in the vendor master. Defaults to <see langword="true"/>.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Contact email address, when known (redacted in logs).</summary>
    public string? ContactEmail { get; init; }

    /// <summary>Returns <see langword="true"/> when the vendor holds the named certification (case-insensitive).</summary>
    public bool Holds(string certification) =>
        Certifications.Any(c => string.Equals(c, certification, StringComparison.OrdinalIgnoreCase));
}
