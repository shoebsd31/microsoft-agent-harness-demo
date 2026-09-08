using System.ComponentModel.DataAnnotations;

namespace ProcurementCopilot.Application.Configuration;

/// <summary>Selects the data backend.</summary>
public sealed class DataOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Data";

    /// <summary>Use the SQL Server backend when a connection string is configured and reachable, otherwise the JSON seed files.</summary>
    public const string Auto = "Auto";

    /// <summary>Always use the JSON seed files under <c>data/</c>.</summary>
    public const string Json = "Json";

    /// <summary>Always use SQL Server (AdventureWorks + Procurement schema); start-up fails when it is unreachable.</summary>
    public const string SqlServer = "SqlServer";

    /// <summary><c>Auto</c>, <c>Json</c> or <c>SqlServer</c>.</summary>
    [RegularExpression("^(Auto|Json|SqlServer)$", ErrorMessage = "Data:Provider must be Auto, Json or SqlServer.")]
    public string Provider { get; set; } = Auto;
}

/// <summary>SQL Server backend settings (AdventureWorks2019 with the Procurement and copilot schemas).</summary>
public sealed class SqlServerOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "SqlServer";

    /// <summary>Connection string. Prefer integrated security; put SQL logins in user-secrets, never in appsettings.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Command timeout for every query, including <c>query_readonly</c>.</summary>
    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 15;

    /// <summary>Maximum rows returned by <c>query_readonly</c>; extra rows are dropped and reported as truncated.</summary>
    [Range(1, 5_000)]
    public int QueryRowLimit { get; set; } = 200;

    /// <summary>Database user impersonated for <c>query_readonly</c> (created by migration 0003 with SELECT on the copilot schema only).</summary>
    [Required]
    public string ReadOnlyUser { get; set; } = "copilot_reader";

    /// <summary>AdventureWorks employee recorded on purchase orders created by <c>record_award_recommendation</c>.</summary>
    public int PurchaseOrderEmployeeId { get; set; } = 261;

    /// <summary>AdventureWorks ship method recorded on purchase orders created by <c>record_award_recommendation</c>.</summary>
    public int ShipMethodId { get; set; } = 1;

    /// <summary>Folder with the versioned migration scripts, relative to the repository root or absolute.</summary>
    [Required]
    public string MigrationsDirectory { get; set; } = "database/migrations";

    /// <summary>Gets a value indicating whether a connection string is present.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}
