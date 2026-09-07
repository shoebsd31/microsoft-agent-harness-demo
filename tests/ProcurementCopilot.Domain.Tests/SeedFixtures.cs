using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Tests;

/// <summary>In-code copy of the seeded RFP-2026-017 data so the domain tests need no I/O.</summary>
public static class SeedFixtures
{
    public static Rfp Rfp017() => new(
        RfpId.Create("RFP-2026-017").Value, "Supply of 40 CNC vertical machining centres", "desc", 40, CurrencyCode.Eur, RfpStatus.Open,
        new EvaluationCriteria(35, 20, 15, 20, 10), ["ISO 9001", "CE"], "Machine Tools");

    public static Dictionary<VendorId, Vendor> Vendors() => new[]
    {
        Vendor("VND-0001", "Müller", "Germany", ["ISO 9001", "ISO 14001", "CE"], 32),
        Vendor("VND-0002", "Nordic CNC", "Sweden", ["ISO 9001", "CE"], 15),
        Vendor("VND-0003", "Apex", "United States", ["ISO 9001", "ISO 14001", "CE"], 21),
        Vendor("VND-0004", "Veldora", "Veldora", ["ISO 9001", "ISO 14001", "CE"], 27),
        Vendor("VND-0005", "Kyoto Precision", "Japan", ["ISO 9001", "ISO 14001", "CE"], 40),
    }.ToDictionary(v => v.Id);

    public static List<Bid> Bids() =>
    [
        Bid("BID-001", "VND-0001", 185000m, "EUR", 16, 24, 96),
        Bid("BID-002", "VND-0002", 168500m, "EUR", 20, 18, 91),
        Bid("BID-003", "VND-0003", 199000m, "USD", 14, 36, 94),
        Bid("BID-004", "VND-0004", 149900m, "EUR", 12, 12, 88),
        Bid("BID-005", "VND-0005", 176000m, "EUR", 18, 30, 98),
    ];

    public static List<FxRate> Rates() => [new FxRate(CurrencyCode.Usd, CurrencyCode.Eur, 0.92m, new DateOnly(2026, 8, 29))];

    public static List<SanctionsEntry> Sanctions() => [new SanctionsEntry(VendorId.Create("VND-0004").Value, "Contoso Restricted Parties List", "designated", new DateOnly(2026, 3, 2))];

    public static Vendor Vendor(string id, string name, string country, string[] certs, int years) =>
        new(VendorId.Create(id).Value, name, country, certs, years, "notes");

    public static Bid Bid(string id, string vendor, decimal price, string currency, int lead, int warranty, decimal technical) =>
        new(BidId.Create(id).Value, RfpId.Create("RFP-2026-017").Value, VendorId.Create(vendor).Value, Money.Create(price, currency).Value, lead, warranty, technical, "clause", "notes");
}
