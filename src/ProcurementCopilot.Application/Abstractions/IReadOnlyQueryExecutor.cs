using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Application.Abstractions;

/// <summary>A tabular query result, capped by the executor.</summary>
/// <param name="Columns">Column names in order.</param>
/// <param name="Rows">Row values as text (nulls as <see langword="null"/>).</param>
/// <param name="Truncated">Whether more rows were available than the limit.</param>
/// <param name="ElapsedMilliseconds">Execution time.</param>
public sealed record QueryResultSet(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows, bool Truncated, long ElapsedMilliseconds);

/// <summary>
/// Runs a SELECT that has already passed <c>SqlQueryPolicy</c>, under a least-privilege database user, with a hard
/// timeout and a row cap. Unavailable when the JSON backend is active.
/// </summary>
public interface IReadOnlyQueryExecutor
{
    /// <summary>Gets a value indicating whether a database is configured and the tool should be offered to the model.</summary>
    bool IsAvailable { get; }

    /// <summary>Short description of the sandbox (user, schema, limits) for the tool description.</summary>
    string Description { get; }

    /// <summary>Executes the query and returns at most <paramref name="rowLimit"/> rows.</summary>
    Task<Result<QueryResultSet>> ExecuteAsync(string sql, int rowLimit, CancellationToken cancellationToken = default);
}

/// <summary>Describes which data backend is active, for the banner, the self-check and the tools.</summary>
public interface IDataProviderInfo
{
    /// <summary>Gets the provider name: <c>Json</c> or <c>SqlServer</c>.</summary>
    string Provider { get; }

    /// <summary>Gets a human-readable description (file path or server/database, never credentials).</summary>
    string Description { get; }
}
