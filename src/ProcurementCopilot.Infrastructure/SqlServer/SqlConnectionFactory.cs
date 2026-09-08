using Microsoft.Data.SqlClient;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Infrastructure.SqlServer;

/// <summary>Creates connections to the AdventureWorks database from <see cref="SqlServerOptions"/>.</summary>
public sealed class SqlConnectionFactory
{
    /// <summary>Initializes the factory.</summary>
    public SqlConnectionFactory(SqlServerOptions options) => Options = options;

    /// <summary>Gets the options.</summary>
    public SqlServerOptions Options { get; }

    /// <summary>Gets the command timeout in seconds.</summary>
    public int CommandTimeout => Options.CommandTimeoutSeconds;

    /// <summary>Creates an unopened connection.</summary>
    public SqlConnection Create() => new(Options.ConnectionString);

    /// <summary>Describes the target without credentials, e.g. <c>localhost\SQLEXPRESS/AdventureWorks2019</c>.</summary>
    public string Describe()
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(Options.ConnectionString);
            return $"{builder.DataSource}/{builder.InitialCatalog}";
        }
        catch (ArgumentException)
        {
            return "(invalid connection string)";
        }
    }
}

/// <summary>Maps AdventureWorks <c>BusinessEntityID</c>s to the <c>VND-nnnn</c> ids used by the domain and the tools.</summary>
public static class VendorIdMap
{
    /// <summary>Formats a BusinessEntityID as <c>VND-nnnn</c>.</summary>
    public static VendorId ToVendorId(int businessEntityId) =>
        VendorId.Create($"VND-{businessEntityId:0000}").Value;

    /// <summary>Extracts the BusinessEntityID from a <c>VND-nnnn</c> id.</summary>
    public static int ToBusinessEntityId(VendorId vendorId) =>
        int.Parse(vendorId.Value.AsSpan(4), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Parses a raw string into a BusinessEntityID, failing on bad formats.</summary>
    public static Result<int> ParseBusinessEntityId(string? vendorId) =>
        VendorId.Create(vendorId).Map(ToBusinessEntityId);
}
