using Dapper;
using Microsoft.Data.SqlClient;
using ProcurementCopilot.Application.Abstractions;

namespace ProcurementCopilot.Infrastructure.SqlServer;

/// <summary>Vendors, restricted parties, awards and product search.</summary>
public sealed partial class ProcurementAdminStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminVendor>> ListVendorsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT v.BusinessEntityID AS BusinessEntityId, v.VendorId, v.VendorName AS Name, v.CreditRating, v.ActiveFlag AS Active, v.Country,
                   vp.CountryOverride, v.YearsTrading, v.Notes, ISNULL(v.Certifications, '') AS Certifications, v.IsRestricted
            FROM copilot.Vendors v
            LEFT JOIN Procurement.VendorProfile vp ON vp.BusinessEntityID = v.BusinessEntityID
            ORDER BY v.VendorName
            """;
        await using SqlConnection c = _factory.Create();
        return (await c.QueryAsync<AdminVendor>(Cmd(sql, ct: ct)).ConfigureAwait(false)).ToList();
    }

    /// <inheritdoc />
    public async Task SaveVendorProfileAsync(AdminVendor vendor, CancellationToken ct = default)
    {
        const string sql = """
            MERGE Procurement.VendorProfile AS t
            USING (SELECT @BusinessEntityId AS BusinessEntityID) AS s ON t.BusinessEntityID = s.BusinessEntityID
            WHEN MATCHED THEN UPDATE SET CountryOverride = @CountryOverride, YearsTrading = @YearsTrading, Notes = @Notes, ModifiedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (BusinessEntityID, CountryOverride, YearsTrading, Notes) VALUES (@BusinessEntityId, @CountryOverride, @YearsTrading, @Notes);
            DELETE FROM Procurement.VendorCertification WHERE BusinessEntityID = @BusinessEntityId;
            """;
        await using SqlConnection c = _factory.Create();
        await c.OpenAsync(ct).ConfigureAwait(false);
        await using SqlTransaction tx = (SqlTransaction)await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        await c.ExecuteAsync(Cmd(sql, new { vendor.BusinessEntityId, CountryOverride = NullIfBlank(vendor.CountryOverride), vendor.YearsTrading, Notes = NullIfBlank(vendor.Notes) }, ct, tx)).ConfigureAwait(false);
        foreach (string cert in SplitList(vendor.Certifications))
        {
            await c.ExecuteAsync(Cmd("INSERT INTO Procurement.VendorCertification (BusinessEntityID, Certification) VALUES (@BusinessEntityId, @cert)", new { vendor.BusinessEntityId, cert }, ct, tx)).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminRestrictedParty>> ListRestrictedPartiesAsync(CancellationToken ct = default)
    {
        await using SqlConnection c = _factory.Create();
        return (await c.QueryAsync<AdminRestrictedParty>(Cmd("SELECT BusinessEntityID AS BusinessEntityId, VendorName, ListName, ListedOn, Reason FROM copilot.RestrictedParties ORDER BY VendorName", ct: ct)).ConfigureAwait(false)).ToList();
    }

    /// <inheritdoc />
    public async Task SaveRestrictedPartyAsync(AdminRestrictedParty entry, CancellationToken ct = default)
    {
        const string sql = """
            MERGE Procurement.RestrictedParty AS t
            USING (SELECT @BusinessEntityId AS BusinessEntityID) AS s ON t.BusinessEntityID = s.BusinessEntityID
            WHEN MATCHED THEN UPDATE SET ListName = @ListName, ListedOn = @ListedOn, Reason = @Reason
            WHEN NOT MATCHED THEN INSERT (BusinessEntityID, ListName, ListedOn, Reason) VALUES (@BusinessEntityId, @ListName, @ListedOn, @Reason);
            """;
        await using SqlConnection c = _factory.Create();
        await c.ExecuteAsync(Cmd(sql, new { entry.BusinessEntityId, entry.ListName, ListedOn = entry.ListedOn.Date, entry.Reason }, ct)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteRestrictedPartyAsync(int businessEntityId, CancellationToken ct = default)
    {
        await using SqlConnection c = _factory.Create();
        await c.ExecuteAsync(Cmd("DELETE FROM Procurement.RestrictedParty WHERE BusinessEntityID = @businessEntityId", new { businessEntityId }, ct)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminAward>> ListAwardsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT a.AwardRecommendationID AS AwardRecommendationId, a.RfpId, a.VendorId, a.VendorName, a.PurchaseOrderID AS PurchaseOrderId, a.WinningScore, a.Rationale, a.RecordedAt, a.RecordedBy,
                   po.StatusName AS PurchaseOrderStatus, po.TotalDue
            FROM copilot.AwardRecommendations a
            LEFT JOIN copilot.PurchaseOrders po ON po.PurchaseOrderID = a.PurchaseOrderID
            ORDER BY a.RecordedAt DESC
            """;
        await using SqlConnection c = _factory.Create();
        return (await c.QueryAsync<AdminAward>(Cmd(sql, ct: ct)).ConfigureAwait(false)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductPick>> SearchProductsAsync(string term, CancellationToken ct = default)
    {
        const string sql = "SELECT TOP 25 ProductID AS ProductId, ProductName AS Name, ProductNumber, Subcategory FROM copilot.Products WHERE ProductName LIKE @pattern OR ProductNumber LIKE @pattern ORDER BY ProductName";
        await using SqlConnection c = _factory.Create();
        return (await c.QueryAsync<ProductPick>(Cmd(sql, new { pattern = "%" + term.Trim() + "%" }, ct)).ConfigureAwait(false)).ToList();
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
