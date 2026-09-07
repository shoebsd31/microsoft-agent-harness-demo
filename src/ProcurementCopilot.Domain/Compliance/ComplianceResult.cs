using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Compliance;

/// <summary>Outcome of checking a vendor against the sanctions list and required certifications.</summary>
/// <param name="VendorId">The vendor checked.</param>
/// <param name="Sanction">The sanctions entry when the vendor is listed, otherwise <see langword="null"/>.</param>
/// <param name="MissingCertifications">Required certifications the vendor does not hold.</param>
public sealed record ComplianceResult(VendorId VendorId, SanctionsEntry? Sanction, IReadOnlyList<string> MissingCertifications)
{
    /// <summary>Gets a value indicating whether the vendor is on the sanctions list.</summary>
    public bool IsSanctioned => Sanction is not null;

    /// <summary>Gets a value indicating whether there is at least one compliance issue.</summary>
    public bool HasIssues => IsSanctioned || MissingCertifications.Count > 0;

    /// <summary>Gets a short human-readable verdict.</summary>
    public string Verdict => IsSanctioned
        ? "BLOCKED: vendor is on the sanctions list"
        : MissingCertifications.Count > 0
            ? $"GAP: missing {string.Join(", ", MissingCertifications)}"
            : "PASS";
}
