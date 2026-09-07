using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Entities;

/// <summary>A vendor listed on the (seeded) sanctions list.</summary>
/// <param name="VendorId">The sanctioned vendor.</param>
/// <param name="ListName">Which list the entry comes from.</param>
/// <param name="Reason">Reason recorded on the list.</param>
/// <param name="ListedOn">Date the vendor was listed.</param>
public sealed record SanctionsEntry(VendorId VendorId, string ListName, string Reason, DateOnly ListedOn);
