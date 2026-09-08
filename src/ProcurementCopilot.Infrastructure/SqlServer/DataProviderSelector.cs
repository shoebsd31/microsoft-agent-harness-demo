using Microsoft.Data.SqlClient;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Configuration;

namespace ProcurementCopilot.Infrastructure.SqlServer;

/// <summary>Outcome of choosing the data backend at start-up.</summary>
/// <param name="Provider"><c>Json</c> or <c>SqlServer</c>.</param>
/// <param name="Description">Human-readable target (never credentials).</param>
/// <param name="Warning">Why <c>Auto</c> fell back to JSON, when it did.</param>
public sealed record DataProviderSelection(string Provider, string Description, string? Warning) : IDataProviderInfo
{
    /// <summary>Gets a value indicating whether SQL Server is active.</summary>
    public bool IsSqlServer => Provider == DataOptions.SqlServer;
}

/// <summary>
/// Resolves <c>Data:Provider</c>: <c>Json</c> and <c>SqlServer</c> are explicit; <c>Auto</c> probes the connection once
/// (short timeout) and falls back to the JSON seed files with a warning when the database is unreachable or not migrated.
/// </summary>
public static class DataProviderSelector
{
    /// <summary>Chooses the backend.</summary>
    public static DataProviderSelection Resolve(DataOptions data, SqlServerOptions sql, string jsonDirectory)
    {
        string jsonDescription = $"JSON seed files in {jsonDirectory}";
        if (data.Provider == DataOptions.Json)
        {
            return new DataProviderSelection(DataOptions.Json, jsonDescription, null);
        }

        if (!sql.IsConfigured)
        {
            return data.Provider == DataOptions.SqlServer
                ? throw new InvalidOperationException("Data:Provider is SqlServer but SqlServer:ConnectionString is empty.")
                : new DataProviderSelection(DataOptions.Json, jsonDescription, "SqlServer:ConnectionString is empty; using the JSON seed files.");
        }

        string? failure = Probe(sql);
        if (failure is null)
        {
            return new DataProviderSelection(DataOptions.SqlServer, $"SQL Server {new SqlConnectionFactory(sql).Describe()} (Procurement + copilot schemas)", null);
        }

        return data.Provider == DataOptions.SqlServer
            ? throw new InvalidOperationException("Data:Provider is SqlServer but the database is not usable: " + failure)
            : new DataProviderSelection(DataOptions.Json, jsonDescription, "SQL Server is not usable (" + failure + "); using the JSON seed files.");
    }

    /// <summary>Opens a connection with a short timeout and checks that the copilot schema exists. Returns <see langword="null"/> on success.</summary>
    public static string? Probe(SqlServerOptions sql)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(sql.ConnectionString) { ConnectTimeout = Math.Min(5, Math.Max(1, sql.CommandTimeoutSeconds)) };
            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
            using var command = new SqlCommand("SELECT COUNT(*) FROM sys.views WHERE schema_id = SCHEMA_ID('copilot')", connection) { CommandTimeout = 5 };
            int views = (int)command.ExecuteScalar()!;
            return views == 0 ? "the copilot schema is missing; run the migrations (scripts/apply-migrations.ps1)" : null;
        }
        catch (SqlException ex)
        {
            return "SQL error " + ex.Number + ": " + ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
        catch (ArgumentException ex)
        {
            return "invalid connection string: " + ex.Message;
        }
    }
}
