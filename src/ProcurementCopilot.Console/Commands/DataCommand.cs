using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.Infrastructure.SqlServer;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Commands;

/// <summary><c>/data</c>: which data backend is active and, for SQL Server, the migration status.</summary>
public sealed class DataCommand(DataProviderSelection data, SqlMigrationRunner migrations) : IConsoleCommand
{
    /// <inheritdoc />
    public string Name => "/data";

    /// <inheritdoc />
    public string Help => "show the data backend (JSON seed files or SQL Server) and migration status";

    /// <inheritdoc />
    public async Task ExecuteAsync(string[] args, AppState state)
    {
        var table = new Table().Border(TableBorder.Simple).AddColumn("Item").AddColumn("Value");
        table.AddRow("Provider", Markup.Escape(data.Provider));
        table.AddRow("Target", Markup.Escape(data.Description));
        if (data.Warning is not null)
        {
            table.AddRow("Warning", "[yellow]" + Markup.Escape(data.Warning) + "[/]");
        }

        if (data.IsSqlServer)
        {
            MigrationStatus status = await migrations.GetStatusAsync().ConfigureAwait(false);
            table.AddRow("Migrations", status.IsUpToDate ? $"[green]{status.Applied.Count} applied, up to date[/]" : $"[yellow]{status.Pending.Count} pending[/]: " + Markup.Escape(string.Join(", ", status.Pending)));
            table.AddRow("Scripts", Markup.Escape(status.Directory));
            table.AddRow("query_readonly", "available (approval-gated, copilot schema, copilot_reader user)");
        }
        else
        {
            table.AddRow("query_readonly", "not available with the JSON backend");
        }

        AnsiConsole.Write(table);
    }
}
