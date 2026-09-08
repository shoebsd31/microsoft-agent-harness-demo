using ProcurementCopilot.Admin.Components;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Infrastructure.DependencyInjection;
using ProcurementCopilot.Infrastructure.SqlServer;

// Procurement Copilot Admin: Blazor Server CRUD over the Procurement schema (RFPs, bids, vendor profiles,
// restricted parties, awards) plus migration status. Flags: --migrate applies pending scripts and exits,
// --status prints the migration status and exits.
bool migrate = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
bool statusOnly = args.Contains("--status", StringComparer.OrdinalIgnoreCase);
string[] hostArgs = args.Where(a => !string.Equals(a, "--migrate", StringComparison.OrdinalIgnoreCase) && !string.Equals(a, "--status", StringComparison.OrdinalIgnoreCase)).ToArray();
WebApplicationBuilder builder = WebApplication.CreateBuilder(hostArgs);
builder.Services.AddProcurementOptions(builder.Configuration);
builder.Services.AddDataBackend(builder.Configuration);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

WebApplication app = builder.Build();

if (migrate || statusOnly)
{
    return await RunMigrationCommandAsync(app.Services, apply: migrate).ConfigureAwait(false);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
await app.RunAsync().ConfigureAwait(false);
return 0;

static async Task<int> RunMigrationCommandAsync(IServiceProvider services, bool apply)
{
    SqlMigrationRunner runner = services.GetRequiredService<SqlMigrationRunner>();
    DataProviderSelection data = services.GetRequiredService<DataProviderSelection>();
    Console.WriteLine($"Database: {data.Description}");
    if (apply)
    {
        IReadOnlyList<string> applied = await runner.ApplyPendingAsync().ConfigureAwait(false);
        Console.WriteLine(applied.Count == 0 ? "No pending migrations." : "Applied: " + string.Join(", ", applied));
    }

    MigrationStatus status = await runner.GetStatusAsync().ConfigureAwait(false);
    Console.WriteLine($"Scripts folder: {status.Directory}");
    Console.WriteLine($"Applied ({status.Applied.Count}): {string.Join(", ", status.Applied)}");
    Console.WriteLine($"Pending ({status.Pending.Count}): {string.Join(", ", status.Pending)}");
    return status.IsUpToDate ? 0 : 1;
}
