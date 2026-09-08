using Dapper;
using Microsoft.Data.SqlClient;
using ProcurementCopilot.Application.Abstractions;

namespace ProcurementCopilot.Infrastructure.SqlServer;

/// <summary>Dapper implementation of <see cref="IProcurementAdminStore"/> (RFPs and bids).</summary>
public sealed partial class ProcurementAdminStore : IProcurementAdminStore
{
    private readonly SqlConnectionFactory _factory;

    /// <summary>Initializes the store.</summary>
    public ProcurementAdminStore(SqlConnectionFactory factory) => _factory = factory;

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminRfp>> ListRfpsAsync(CancellationToken ct = default)
    {
        await using SqlConnection c = _factory.Create();
        return (await c.QueryAsync<AdminRfp>(Cmd(RfpSelect + " ORDER BY RfpId", ct: ct)).ConfigureAwait(false)).ToList();
    }

    /// <inheritdoc />
    public async Task<AdminRfp?> GetRfpAsync(string rfpId, CancellationToken ct = default)
    {
        await using SqlConnection c = _factory.Create();
        return await c.QuerySingleOrDefaultAsync<AdminRfp>(Cmd(RfpSelect + " WHERE RfpId = @rfpId", new { rfpId }, ct)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveRfpAsync(AdminRfp rfp, CancellationToken ct = default)
    {
        const string sql = """
            MERGE Procurement.Rfp AS t
            USING (SELECT @RfpId AS RfpId) AS s ON t.RfpId = s.RfpId
            WHEN MATCHED THEN UPDATE SET Title = @Title, Description = @Description, ProductID = @ProductId, Quantity = @Quantity, CurrencyCode = @CurrencyCode,
                 Status = @Status, Category = @Category, WeightPrice = @WeightPrice, WeightLeadTime = @WeightLeadTime, WeightWarranty = @WeightWarranty,
                 WeightTechnical = @WeightTechnical, WeightSustainability = @WeightSustainability, ModifiedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (RfpId, Title, Description, ProductID, Quantity, CurrencyCode, Status, Category, WeightPrice, WeightLeadTime, WeightWarranty, WeightTechnical, WeightSustainability)
                 VALUES (@RfpId, @Title, @Description, @ProductId, @Quantity, @CurrencyCode, @Status, @Category, @WeightPrice, @WeightLeadTime, @WeightWarranty, @WeightTechnical, @WeightSustainability);
            DELETE FROM Procurement.RfpRequiredCertification WHERE RfpId = @RfpId;
            """;
        await using SqlConnection c = _factory.Create();
        await c.OpenAsync(ct).ConfigureAwait(false);
        await using SqlTransaction tx = (SqlTransaction)await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        await c.ExecuteAsync(Cmd(sql, rfp, ct, tx)).ConfigureAwait(false);
        foreach (string cert in SplitList(rfp.RequiredCertifications))
        {
            await c.ExecuteAsync(Cmd("INSERT INTO Procurement.RfpRequiredCertification (RfpId, Certification) VALUES (@RfpId, @cert)", new { rfp.RfpId, cert }, ct, tx)).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteRfpAsync(string rfpId, CancellationToken ct = default)
    {
        await using SqlConnection c = _factory.Create();
        await c.ExecuteAsync(Cmd("DELETE FROM Procurement.AwardRecommendation WHERE RfpId = @rfpId; DELETE FROM Procurement.Rfp WHERE RfpId = @rfpId", new { rfpId }, ct)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminBid>> ListBidsAsync(string? rfpId, CancellationToken ct = default)
    {
        const string sql = "SELECT BidNumber, BidId, RfpId, BusinessEntityID AS BusinessEntityId, VendorName, UnitPrice, CurrencyCode, LeadTimeWeeks, WarrantyMonths, TechnicalCompliancePercent, DeliveryClause, Notes FROM copilot.Bids WHERE (@rfpId IS NULL OR RfpId = @rfpId) ORDER BY RfpId, BidNumber";
        await using SqlConnection c = _factory.Create();
        return (await c.QueryAsync<AdminBid>(Cmd(sql, new { rfpId }, ct)).ConfigureAwait(false)).ToList();
    }

    /// <inheritdoc />
    public async Task SaveBidAsync(AdminBid bid, CancellationToken ct = default)
    {
        const string insert = """
            INSERT INTO Procurement.Bid (RfpId, BusinessEntityID, UnitPrice, CurrencyCode, LeadTimeWeeks, WarrantyMonths, TechnicalCompliancePercent, DeliveryClause, Notes)
            VALUES (@RfpId, @BusinessEntityId, @UnitPrice, @CurrencyCode, @LeadTimeWeeks, @WarrantyMonths, @TechnicalCompliancePercent, @DeliveryClause, @Notes)
            """;
        const string update = """
            UPDATE Procurement.Bid SET RfpId = @RfpId, BusinessEntityID = @BusinessEntityId, UnitPrice = @UnitPrice, CurrencyCode = @CurrencyCode, LeadTimeWeeks = @LeadTimeWeeks,
                WarrantyMonths = @WarrantyMonths, TechnicalCompliancePercent = @TechnicalCompliancePercent, DeliveryClause = @DeliveryClause, Notes = @Notes, ModifiedAt = SYSUTCDATETIME()
            WHERE BidNumber = @BidNumber
            """;
        await using SqlConnection c = _factory.Create();
        await c.ExecuteAsync(Cmd(bid.BidNumber == 0 ? insert : update, bid, ct)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteBidAsync(int bidNumber, CancellationToken ct = default)
    {
        await using SqlConnection c = _factory.Create();
        await c.ExecuteAsync(Cmd("DELETE FROM Procurement.Bid WHERE BidNumber = @bidNumber", new { bidNumber }, ct)).ConfigureAwait(false);
    }

    private const string RfpSelect = "SELECT RfpId, Title, Description, ProductID AS ProductId, ProductName, Quantity, CurrencyCode, Status, Category, WeightPrice, WeightLeadTime, WeightWarranty, WeightTechnical, WeightSustainability, ISNULL(RequiredCertifications, '') AS RequiredCertifications, BidCount FROM copilot.Rfps";

    private CommandDefinition Cmd(string sql, object? parameters = null, CancellationToken ct = default, SqlTransaction? tx = null) =>
        new(sql, parameters, tx, _factory.CommandTimeout, cancellationToken: ct);

    private static IEnumerable<string> SplitList(string? csv) =>
        (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase);
}
