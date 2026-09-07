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
    /// <summary>Returns <see langword="true"/> when the vendor holds the named certification (case-insensitive).</summary>
    public bool Holds(string certification) =>
        Certifications.Any(c => string.Equals(c, certification, StringComparison.OrdinalIgnoreCase));
}
