using Dapper;
using Microsoft.Data.SqlClient;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Infrastructure.SqlServer;

/// <summary>Maps view rows to domain entities. Currency codes outside the allowlist fail loudly (data error).</summary>
internal static class SqlMappers
{
    public static Rfp ToRfp(RfpRow r) => new(
        RfpId.Create(r.RfpId).Value, r.Title, r.Description, r.Quantity, CurrencyCode.Create(r.CurrencyCode).Value,
        Enum.Parse<RfpStatus>(r.Status, ignoreCase: true),
        new EvaluationCriteria(r.WeightPrice, r.WeightLeadTime, r.WeightWarranty, r.WeightTechnical, r.WeightSustainability),
        Split(r.RequiredCertifications), r.Category);

    public static Vendor ToVendor(VendorRow r) => new(
        VendorIdMap.ToVendorId(r.BusinessEntityID), r.VendorName, r.Country, Split(r.Certifications), r.YearsTrading ?? 0, r.Notes ?? string.Empty)
    {
        CreditRating = r.CreditRating,
        IsPreferred = r.PreferredVendorStatus,
        IsActive = r.ActiveFlag,
        ContactEmail = r.ContactEmail,
    };

    public static Bid ToBid(BidRow r) => new(
        BidId.Create(r.BidId).Value, RfpId.Create(r.RfpId).Value, VendorIdMap.ToVendorId(r.BusinessEntityID),
        Money.Create(r.UnitPrice, r.CurrencyCode).Value, r.LeadTimeWeeks, r.WarrantyMonths, r.TechnicalCompliancePercent, r.DeliveryClause, r.Notes);

    public static SanctionsEntry ToSanction(RestrictedPartyRow r) =>
        new(VendorIdMap.ToVendorId(r.BusinessEntityID), r.ListName, r.Reason, DateOnly.FromDateTime(r.ListedOn));

    public static FxRate? ToFxRate(CurrencyRateRow r) =>
        CurrencyCode.Create(r.FromCurrencyCode) is { IsSuccess: true } from && CurrencyCode.Create(r.ToCurrencyCode) is { IsSuccess: true } to
            ? new FxRate(from.Value, to.Value, r.Rate, DateOnly.FromDateTime(r.AsOf))
            : null;

    private static IReadOnlyList<string> Split(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>RFPs from <c>copilot.Rfps</c>.</summary>
public sealed class SqlRfpRepository(SqlConnectionFactory factory) : IRfpRepository
{
    private const string Select = "SELECT RfpId, Title, Description, ProductID, ProductName, Quantity, CurrencyCode, Status, Category, WeightPrice, WeightLeadTime, WeightWarranty, WeightTechnical, WeightSustainability, RequiredCertifications FROM copilot.Rfps";

    /// <inheritdoc />
    public async Task<IReadOnlyList<Rfp>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = factory.Create();
        IEnumerable<RfpRow> rows = await connection.QueryAsync<RfpRow>(new CommandDefinition(Select + " ORDER BY RfpId", commandTimeout: factory.CommandTimeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(SqlMappers.ToRfp).ToList();
    }

    /// <inheritdoc />
    public async Task<Rfp?> GetByIdAsync(RfpId id, CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = factory.Create();
        RfpRow? row = await connection.QuerySingleOrDefaultAsync<RfpRow>(new CommandDefinition(Select + " WHERE RfpId = @id", new { id = id.Value }, commandTimeout: factory.CommandTimeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : SqlMappers.ToRfp(row);
    }
}

/// <summary>Vendors from <c>copilot.Vendors</c>.</summary>
public sealed class SqlVendorRepository(SqlConnectionFactory factory) : IVendorRepository
{
    private const string Select = "SELECT BusinessEntityID, VendorName, CreditRating, PreferredVendorStatus, ActiveFlag, Country, YearsTrading, Notes, Certifications, ContactEmail FROM copilot.Vendors";

    /// <inheritdoc />
    public async Task<IReadOnlyList<Vendor>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = factory.Create();
        IEnumerable<VendorRow> rows = await connection.QueryAsync<VendorRow>(new CommandDefinition(Select + " ORDER BY BusinessEntityID", commandTimeout: factory.CommandTimeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(SqlMappers.ToVendor).ToList();
    }

    /// <inheritdoc />
    public async Task<Vendor?> GetByIdAsync(VendorId id, CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = factory.Create();
        VendorRow? row = await connection.QuerySingleOrDefaultAsync<VendorRow>(new CommandDefinition(Select + " WHERE BusinessEntityID = @id", new { id = VendorIdMap.ToBusinessEntityId(id) }, commandTimeout: factory.CommandTimeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : SqlMappers.ToVendor(row);
    }
}

/// <summary>Bids from <c>copilot.Bids</c>.</summary>
public sealed class SqlBidRepository(SqlConnectionFactory factory) : IBidRepository
{
    private const string Select = "SELECT BidId, RfpId, BusinessEntityID, UnitPrice, CurrencyCode, LeadTimeWeeks, WarrantyMonths, TechnicalCompliancePercent, DeliveryClause, Notes FROM copilot.Bids";

    /// <inheritdoc />
    public async Task<IReadOnlyList<Bid>> GetByRfpAsync(RfpId rfpId, CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = factory.Create();
        IEnumerable<BidRow> rows = await connection.QueryAsync<BidRow>(new CommandDefinition(Select + " WHERE RfpId = @id ORDER BY BidNumber", new { id = rfpId.Value }, commandTimeout: factory.CommandTimeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(SqlMappers.ToBid).ToList();
    }

    /// <inheritdoc />
    public async Task<Bid?> GetByIdAsync(BidId id, CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = factory.Create();
        BidRow? row = await connection.QuerySingleOrDefaultAsync<BidRow>(new CommandDefinition(Select + " WHERE BidId = @id", new { id = id.Value }, commandTimeout: factory.CommandTimeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : SqlMappers.ToBid(row);
    }
}

/// <summary>Restricted parties from <c>copilot.RestrictedParties</c>.</summary>
public sealed class SqlRestrictedPartyRepository(SqlConnectionFactory factory) : ISanctionsRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SanctionsEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = factory.Create();
        IEnumerable<RestrictedPartyRow> rows = await connection.QueryAsync<RestrictedPartyRow>(new CommandDefinition("SELECT BusinessEntityID, ListName, ListedOn, Reason FROM copilot.RestrictedParties", commandTimeout: factory.CommandTimeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(SqlMappers.ToSanction).ToList();
    }
}

/// <summary>Latest exchange rates from <c>copilot.CurrencyRates</c> (AdventureWorks <c>Sales.CurrencyRate</c>).</summary>
public sealed class SqlCurrencyRateRepository(SqlConnectionFactory factory) : IFxRateRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FxRate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = factory.Create();
        IEnumerable<CurrencyRateRow> rows = await connection.QueryAsync<CurrencyRateRow>(new CommandDefinition("SELECT FromCurrencyCode, ToCurrencyCode, Rate, AsOf FROM copilot.CurrencyRates", commandTimeout: factory.CommandTimeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(SqlMappers.ToFxRate).Where(r => r is not null).Select(r => r!).ToList();
    }
}
