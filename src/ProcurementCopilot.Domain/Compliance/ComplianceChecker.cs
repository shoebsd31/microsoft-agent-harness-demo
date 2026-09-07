using ProcurementCopilot.Domain.Entities;

namespace ProcurementCopilot.Domain.Compliance;

/// <summary>Pure compliance rules: sanctions membership and required certifications per category.</summary>
public static class ComplianceChecker
{
    /// <summary>Required certifications by procurement category when the RFP does not list its own.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredByCategory =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Machine Tools"] = ["ISO 9001", "CE"],
            ["Industrial Equipment"] = ["ISO 9001", "CE"],
            ["Electronics"] = ["ISO 9001", "CE", "RoHS"],
        };

    /// <summary>Checks a vendor against the sanctions entries and the RFP's required certifications.</summary>
    public static ComplianceResult Check(Vendor vendor, Rfp rfp, IReadOnlyList<SanctionsEntry> sanctions)
    {
        IReadOnlyList<string> required = rfp.RequiredCertifications.Count > 0
            ? rfp.RequiredCertifications
            : RequiredByCategory.TryGetValue(rfp.Category, out IReadOnlyList<string>? byCategory) ? byCategory : [];

        SanctionsEntry? hit = sanctions.FirstOrDefault(s => s.VendorId == vendor.Id);
        List<string> missing = required.Where(c => !vendor.Holds(c)).ToList();
        return new ComplianceResult(vendor.Id, hit, missing);
    }
}
