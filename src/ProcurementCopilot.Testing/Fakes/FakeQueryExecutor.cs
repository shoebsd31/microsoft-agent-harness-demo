using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Testing.Fakes;

/// <summary>Query executor returning a canned result; records every query.</summary>
public sealed class FakeQueryExecutor : IReadOnlyQueryExecutor
{
    /// <summary>Whether the tool should be offered.</summary>
    public bool IsAvailable { get; set; }

    /// <inheritdoc />
    public string Description => "fake executor";

    /// <summary>Columns returned.</summary>
    public List<string> Columns { get; set; } = ["VendorId", "VendorName"];

    /// <summary>Rows returned (before the row limit is applied).</summary>
    public List<IReadOnlyList<string?>> Rows { get; set; } = [["VND-1498", "Trikes, Inc."], ["VND-1632", "Sport Fan Co."]];

    /// <summary>When set, every call fails with this error.</summary>
    public Error? Error { get; set; }

    /// <summary>Queries received.</summary>
    public List<string> Queries { get; } = [];

    /// <inheritdoc />
    public Task<Result<QueryResultSet>> ExecuteAsync(string sql, int rowLimit, CancellationToken cancellationToken = default)
    {
        Queries.Add(sql);
        if (Error is not null)
        {
            return Task.FromResult(Result<QueryResultSet>.Failure(Error));
        }

        bool truncated = Rows.Count > rowLimit;
        return Task.FromResult(Result<QueryResultSet>.Success(new QueryResultSet(Columns, Rows.Take(rowLimit).ToList(), truncated, 3)));
    }
}

/// <summary>Static provider description for tests.</summary>
public sealed class FakeDataProviderInfo(string provider, string description) : IDataProviderInfo
{
    /// <inheritdoc />
    public string Provider => provider;

    /// <inheritdoc />
    public string Description => description;
}
