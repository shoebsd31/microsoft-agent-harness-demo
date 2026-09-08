using System.Text.RegularExpressions;

namespace ProcurementCopilot.Application.Security;

/// <summary>Reasons a query is rejected by <see cref="SqlQueryPolicy"/>.</summary>
public enum SqlQueryPolicyCode
{
    /// <summary>The query is allowed.</summary>
    Allowed,

    /// <summary>Empty or whitespace.</summary>
    EmptyQuery,

    /// <summary>Longer than <see cref="SqlQueryPolicy.MaxLength"/>.</summary>
    TooLong,

    /// <summary>Contains <c>;</c> or a <c>GO</c> batch separator.</summary>
    MultipleStatementsNotAllowed,

    /// <summary>Contains <c>--</c> or <c>/*</c>.</summary>
    CommentsNotAllowed,

    /// <summary>Does not start with <c>SELECT</c> or <c>WITH</c>.</summary>
    MustBeSelect,

    /// <summary>Contains a data-modification, execution, or system keyword.</summary>
    ForbiddenKeyword,

    /// <summary>Uses variables or system functions (<c>@</c>).</summary>
    VariablesNotAllowed,

    /// <summary>Uses a three-part (cross-database) or four-part name.</summary>
    CrossDatabaseNotAllowed,

    /// <summary>References an object outside the <c>copilot</c> schema.</summary>
    SchemaNotAllowed,
}

/// <summary>Outcome of <see cref="SqlQueryPolicy.Evaluate"/>.</summary>
/// <param name="Code">Verdict code.</param>
/// <param name="Reason">Human-readable reason.</param>
/// <param name="Sql">The trimmed query when allowed.</param>
public sealed record SqlQueryVerdict(SqlQueryPolicyCode Code, string Reason, string Sql)
{
    /// <summary>Gets a value indicating whether the query may run.</summary>
    public bool IsAllowed => Code == SqlQueryPolicyCode.Allowed;
}

/// <summary>
/// Allowlist policy for the approval-gated <c>query_readonly</c> tool: one SELECT, no comments, no variables, no
/// execution or DDL/DML keywords, and every FROM/JOIN/APPLY target inside the <c>copilot</c> schema.
/// Defence in depth: the executor additionally impersonates a user that can only SELECT from that schema.
/// </summary>
public sealed partial class SqlQueryPolicy
{
    /// <summary>Maximum query length.</summary>
    public const int MaxLength = 2_000;

    /// <summary>The only schema the model may query.</summary>
    public const string AllowedSchema = "copilot";

    /// <summary>Evaluates a query text.</summary>
    public SqlQueryVerdict Evaluate(string? sql)
    {
        string text = (sql ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Deny(SqlQueryPolicyCode.EmptyQuery, "No query was given.");
        }

        if (text.Length > MaxLength)
        {
            return Deny(SqlQueryPolicyCode.TooLong, $"Queries longer than {MaxLength} characters are rejected.");
        }

        if (text.Contains("--", StringComparison.Ordinal) || text.Contains("/*", StringComparison.Ordinal))
        {
            return Deny(SqlQueryPolicyCode.CommentsNotAllowed, "Comments are not allowed.");
        }

        string stripped = StringLiteral().Replace(text, "''");
        if (stripped.Contains(';', StringComparison.Ordinal) || BatchSeparator().IsMatch(stripped))
        {
            return Deny(SqlQueryPolicyCode.MultipleStatementsNotAllowed, "Only a single statement is allowed (no ';' or GO).");
        }

        if (!Start().IsMatch(stripped))
        {
            return Deny(SqlQueryPolicyCode.MustBeSelect, "Only SELECT (optionally with a WITH clause) is allowed.");
        }

        if (stripped.Contains('@', StringComparison.Ordinal))
        {
            return Deny(SqlQueryPolicyCode.VariablesNotAllowed, "Variables and @@ system functions are not allowed.");
        }

        Match forbidden = ForbiddenKeyword().Match(stripped);
        if (forbidden.Success)
        {
            return Deny(SqlQueryPolicyCode.ForbiddenKeyword, $"'{forbidden.Value.ToUpperInvariant()}' is not allowed in a read-only query.");
        }

        if (MultiPartName().IsMatch(stripped))
        {
            return Deny(SqlQueryPolicyCode.CrossDatabaseNotAllowed, "Three- and four-part names (other databases or servers) are not allowed.");
        }

        var cteNames = new HashSet<string>(CteName().Matches(stripped).Select(m => m.Groups["cte"].Value.Trim('[', ']')), StringComparer.OrdinalIgnoreCase);
        foreach (Match target in SourceTarget().Matches(stripped))
        {
            string name = target.Groups["name"].Value;
            if (name.StartsWith('(') || cteNames.Contains(name.Trim('[', ']')))
            {
                continue;
            }

            if (!AllowedObject().IsMatch(name))
            {
                return Deny(SqlQueryPolicyCode.SchemaNotAllowed, $"'{name}' is outside the {AllowedSchema} schema; query only {AllowedSchema}.* views (see the database-schema skill).");
            }
        }

        return new SqlQueryVerdict(SqlQueryPolicyCode.Allowed, "Allowed.", text);
    }

    private static SqlQueryVerdict Deny(SqlQueryPolicyCode code, string reason) => new(code, reason, string.Empty);

    [GeneratedRegex(@"'(?:[^']|'')*'")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"(?im)^\s*GO\b")]
    private static partial Regex BatchSeparator();

    [GeneratedRegex(@"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Start();

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|MERGE|EXEC|EXECUTE|TRUNCATE|DROP|ALTER|CREATE|GRANT|DENY|REVOKE|BACKUP|RESTORE|SHUTDOWN|RECONFIGURE|DBCC|KILL|WAITFOR|OPENROWSET|OPENQUERY|OPENDATASOURCE|OPENXML|BULK|INTO|REVERT|DECLARE|SET|USE|xp_\w+|sp_\w+|sys|INFORMATION_SCHEMA|master|msdb|tempdb|model)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenKeyword();

    [GeneratedRegex(@"\[?\w+\]?\s*\.\s*\[?\w*\]?\s*\.\s*\[?\w+\]?")]
    private static partial Regex MultiPartName();

    [GeneratedRegex(@"\b(?:FROM|JOIN|APPLY)\s+(?<name>\(|[\w\[\]\.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SourceTarget();

    [GeneratedRegex(@"(?:\bWITH\s+|,\s*)(?<cte>\[?\w+\]?)\s*(?:\([^)]*\))?\s+AS\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex CteName();

    [GeneratedRegex(@"^\[?copilot\]?\.\[?\w+\]?$", RegexOptions.IgnoreCase)]
    private static partial Regex AllowedObject();
}
