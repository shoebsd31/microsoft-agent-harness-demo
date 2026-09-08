using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ProcurementCopilot.Infrastructure.SqlServer;

/// <summary>State of the versioned migration scripts.</summary>
/// <param name="Applied">Scripts recorded in <c>Procurement.SchemaMigration</c>.</param>
/// <param name="Pending">Scripts in the migrations folder that are not recorded yet.</param>
/// <param name="Directory">The folder the scripts were read from.</param>
public sealed record MigrationStatus(IReadOnlyList<string> Applied, IReadOnlyList<string> Pending, string Directory)
{
    /// <summary>Gets a value indicating whether the schema is fully migrated.</summary>
    public bool IsUpToDate => Pending.Count == 0 && Applied.Count > 0;
}

/// <summary>
/// Applies <c>database/migrations/NNNN_*.sql</c> in order, batch by batch (split on <c>GO</c> lines), and records each
/// script in <c>Procurement.SchemaMigration</c>. Scripts are idempotent, so re-running an applied script is harmless.
/// </summary>
public sealed partial class SqlMigrationRunner
{
    private readonly SqlConnectionFactory _factory;

    /// <summary>Initializes the runner.</summary>
    public SqlMigrationRunner(SqlConnectionFactory factory) => _factory = factory;

    /// <summary>Resolves the migrations folder: absolute, or relative to the base directory and its ancestors.</summary>
    public string ResolveDirectory()
    {
        string configured = _factory.Options.MigrationsDirectory;
        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, configured);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, configured);
    }

    /// <summary>Returns the applied and pending scripts.</summary>
    public async Task<MigrationStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        string directory = ResolveDirectory();
        List<string> scripts = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.sql").Select(Path.GetFileName).Select(n => n!).Order(StringComparer.Ordinal).ToList()
            : [];
        await using SqlConnection connection = _factory.Create();
        HashSet<string> applied = (await connection.QueryAsync<string>(new CommandDefinition(
            "IF OBJECT_ID('Procurement.SchemaMigration','U') IS NULL SELECT CONVERT(nvarchar(200), NULL) WHERE 1 = 0 ELSE SELECT ScriptName FROM Procurement.SchemaMigration",
            commandTimeout: _factory.CommandTimeout, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new MigrationStatus(scripts.Where(applied.Contains).ToList(), scripts.Where(s => !applied.Contains(s)).ToList(), directory);
    }

    /// <summary>Applies every pending script in order and returns their names.</summary>
    public async Task<IReadOnlyList<string>> ApplyPendingAsync(CancellationToken cancellationToken = default)
    {
        MigrationStatus status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        var appliedNow = new List<string>();
        foreach (string script in status.Pending)
        {
            await ApplyScriptAsync(Path.Combine(status.Directory, script), cancellationToken).ConfigureAwait(false);
            appliedNow.Add(script);
        }

        return appliedNow;
    }

    private async Task ApplyScriptAsync(string path, CancellationToken cancellationToken)
    {
        string text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        await using SqlConnection connection = _factory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (string batch in BatchSeparator().Split(text).Select(b => b.Trim()).Where(b => b.Length > 0))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = Math.Max(_factory.CommandTimeout, 120) };
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        string name = Path.GetFileName(path);
        await connection.ExecuteAsync(new CommandDefinition(
            "IF NOT EXISTS (SELECT 1 FROM Procurement.SchemaMigration WHERE ScriptName = @name) INSERT INTO Procurement.SchemaMigration (ScriptName) VALUES (@name)",
            new { name }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    [GeneratedRegex(@"^\s*GO\s*(?:--.*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex BatchSeparator();
}
