using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Infrastructure.SqlServer;

/// <summary>
/// Runs an allowlisted SELECT while impersonating the <c>copilot_reader</c> database user (SELECT on the copilot
/// schema only, created by migration 0003), with the configured command timeout and row cap. <c>REVERT</c> always runs
/// before the pooled connection is returned.
/// </summary>
public sealed partial class SqlReadOnlyQueryExecutor : IReadOnlyQueryExecutor
{
    private readonly SqlConnectionFactory _factory;

    /// <summary>Initializes the executor.</summary>
    public SqlReadOnlyQueryExecutor(SqlConnectionFactory factory)
    {
        _factory = factory;
        if (!Identifier().IsMatch(factory.Options.ReadOnlyUser))
        {
            throw new ArgumentException("SqlServer:ReadOnlyUser must be a simple identifier.", nameof(factory));
        }
    }

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public string Description =>
        $"SQL Server {_factory.Describe()}, executed as database user '{_factory.Options.ReadOnlyUser}' (SELECT on the copilot schema only), {_factory.Options.CommandTimeoutSeconds}s timeout, {_factory.Options.QueryRowLimit} row cap";

    /// <inheritdoc />
    public async Task<Result<QueryResultSet>> ExecuteAsync(string sql, int rowLimit, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        await using SqlConnection connection = _factory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await RunAsync(connection, $"EXECUTE AS USER = '{_factory.Options.ReadOnlyUser}';", cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = _factory.CommandTimeout };
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await ReadAsync(reader, rowLimit, stopwatch, cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex)
        {
            return new Error("Sql.Error", $"SQL Server rejected the query (error {ex.Number}): {ex.Message}");
        }
        finally
        {
            await RunAsync(connection, "REVERT;", CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task<Result<QueryResultSet>> ReadAsync(SqlDataReader reader, int rowLimit, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var columns = new List<string>(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        var rows = new List<IReadOnlyList<string?>>();
        bool truncated = false;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (rows.Count >= rowLimit)
            {
                truncated = true;
                break;
            }

            var values = new string?[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                values[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
            }

            rows.Add(values);
        }

        return new QueryResultSet(columns, rows, truncated, stopwatch.ElapsedMilliseconds);
    }

    private static async Task RunAsync(SqlConnection connection, string statement, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(statement, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();
}

/// <summary>Executor used when the JSON backend is active: reports the tool as unavailable.</summary>
public sealed class UnavailableQueryExecutor : IReadOnlyQueryExecutor
{
    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    public string Description => "no database configured";

    /// <inheritdoc />
    public Task<Result<QueryResultSet>> ExecuteAsync(string sql, int rowLimit, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<QueryResultSet>.Failure(new Error("Sql.Unavailable", "No SQL Server backend is configured; query_readonly is not available.")));
}
