using System.Globalization;
using System.Text.Json;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Infrastructure.Data;

/// <summary>Loads and validates the seeded JSON/CSV files once. Invalid seed data is an infrastructure failure and throws.</summary>
public sealed class SeedDataStore
{
    private readonly Lazy<SeedData> _data;

    /// <summary>Initializes the store for a data directory.</summary>
    public SeedDataStore(string dataDirectory)
    {
        DataDirectory = dataDirectory;
        _data = new Lazy<SeedData>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Gets the directory the files are read from.</summary>
    public string DataDirectory { get; }

    /// <summary>Gets the loaded data.</summary>
    public SeedData Data => _data.Value;

    private SeedData Load()
    {
        List<RfpRecord> rfps = ReadJson("rfps.json", SeedJsonContext.Default.ListRfpRecord);
        List<VendorRecord> vendors = ReadJson("vendors.json", SeedJsonContext.Default.ListVendorRecord);
        List<BidRecord> bids = ReadJson("bids.json", SeedJsonContext.Default.ListBidRecord);
        FxRatesFile fx = ReadJson("fx-rates.json", SeedJsonContext.Default.FxRatesFile);

        return new SeedData(
            rfps.Select(MapRfp).ToList(),
            vendors.Select(MapVendor).ToList(),
            bids.Select(MapBid).ToList(),
            ReadSanctions(Path.Combine(DataDirectory, "sanctions.csv")),
            fx.Rates.Select(r => new FxRate(Require(CurrencyCode.Create(r.From)), Require(CurrencyCode.Create(r.To)), r.Rate, DateOnly.Parse(fx.AsOf, CultureInfo.InvariantCulture))).ToList());
    }

    private T ReadJson<T>(string file, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        using FileStream stream = File.OpenRead(Path.Combine(DataDirectory, file));
        return JsonSerializer.Deserialize(stream, typeInfo) ?? throw new InvalidDataException($"{file} is empty.");
    }

    private static Rfp MapRfp(RfpRecord r) => new(
        Require(RfpId.Create(r.Id)), r.Title, r.Description, r.Quantity, Require(CurrencyCode.Create(r.Currency)),
        Enum.Parse<RfpStatus>(r.Status, ignoreCase: true),
        Require(new EvaluationCriteria(r.Criteria.Price, r.Criteria.LeadTime, r.Criteria.Warranty, r.Criteria.Technical, r.Criteria.Sustainability).Validate()),
        r.RequiredCertifications, r.Category);

    private static Vendor MapVendor(VendorRecord v) =>
        new(Require(VendorId.Create(v.Id)), v.Name, v.Country, v.Certifications, v.YearsTrading, v.Notes);

    private static Bid MapBid(BidRecord b) => new(
        Require(BidId.Create(b.Id)), Require(RfpId.Create(b.RfpId)), Require(VendorId.Create(b.VendorId)),
        Require(Money.Create(b.UnitPrice, b.Currency)), b.LeadTimeWeeks, b.WarrantyMonths, b.TechnicalCompliancePercent, b.DeliveryClause, b.Notes);

    private static List<SanctionsEntry> ReadSanctions(string path)
    {
        var entries = new List<SanctionsEntry>();
        foreach (string line in File.ReadLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] cells = line.Split(',', 4);
            if (cells.Length < 4)
            {
                throw new InvalidDataException($"sanctions.csv: expected 4 columns, got '{line}'.");
            }

            entries.Add(new SanctionsEntry(Require(VendorId.Create(cells[0].Trim())), cells[1].Trim(), cells[3].Trim(), DateOnly.Parse(cells[2].Trim(), CultureInfo.InvariantCulture)));
        }

        return entries;
    }

    private static T Require<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw new InvalidDataException($"Seed data is invalid: {result.Error}");
}

/// <summary>The loaded, validated seed data.</summary>
/// <param name="Rfps">RFPs.</param>
/// <param name="Vendors">Vendors.</param>
/// <param name="Bids">Bids.</param>
/// <param name="Sanctions">Sanctions entries.</param>
/// <param name="FxRates">Exchange rates.</param>
public sealed record SeedData(
    IReadOnlyList<Rfp> Rfps,
    IReadOnlyList<Vendor> Vendors,
    IReadOnlyList<Bid> Bids,
    IReadOnlyList<SanctionsEntry> Sanctions,
    IReadOnlyList<FxRate> FxRates);
