using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Infrastructure.SqlServer;

namespace ProcurementCopilot.Integration.Tests;

/// <summary>A fact that is skipped unless the configured SQL Server (appsettings or <c>SqlServer__ConnectionString</c>) is reachable and migrated.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (SqlServerTestConfig.Failure is { } failure)
        {
            Skip = "SQL Server backend not available: " + failure;
        }
    }
}

/// <summary>Resolves the SQL Server options once per test run.</summary>
public static class SqlServerTestConfig
{
    private static readonly Lazy<(SqlServerOptions Options, string? Failure)> Resolved = new(Resolve);

    public static SqlServerOptions Options => Resolved.Value.Options;

    public static string? Failure => Resolved.Value.Failure;

    public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
        .AddEnvironmentVariables()
        .Build();

    private static (SqlServerOptions, string?) Resolve()
    {
        var options = new SqlServerOptions();
        Configuration.GetSection(SqlServerOptions.SectionName).Bind(options);
        if (!options.IsConfigured)
        {
            return (options, "no connection string");
        }

        return (options, DataProviderSelector.Probe(options));
    }
}
