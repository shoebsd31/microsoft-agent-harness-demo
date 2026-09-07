using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProcurementCopilot.Agent.DependencyInjection;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Application.DependencyInjection;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.ConsoleApp.Hosting;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.Infrastructure.DependencyInjection;
using ProcurementCopilot.Infrastructure.Logging;
using Serilog;

// Entry point. Flags: --self-check (validate DI + config without calling a model), --fake (offline scripted demo),
// --session <id> (resume a stored session).
var launch = LaunchOptions.Parse(args);
string logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = launch.HostArgs, ContentRootPath = AppContext.BaseDirectory });
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Configuration.AddEnvironmentVariables();

var redactor = new SecretRedactor(builder.Configuration[$"{FoundryOptions.SectionName}:{nameof(FoundryOptions.ApiKey)}"]);
builder.Services.AddSerilog((_, configuration) => SerilogSetup.Configure(configuration, builder.Configuration, redactor, logsDirectory));

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration, logsDirectory);
builder.Services.AddAgentServices(builder.Configuration);
builder.Services.AddConsoleServices(launch);

using IHost host = builder.Build();
try
{
    HostStartup.ValidateOptions(host.Services);
    HostStartup.StartTelemetry(host.Services);
    if (launch.SelfCheck)
    {
        return await SelfCheck.RunAsync(host.Services).ConfigureAwait(false);
    }

    return await host.Services.GetRequiredService<InteractiveConsole>().RunAsync().ConfigureAwait(false);
}
catch (Exception ex) when (ex is Microsoft.Extensions.Options.OptionsValidationException or InvalidOperationException or AggregateException)
{
    string message = ex is AggregateException aggregate ? string.Join(" | ", aggregate.InnerExceptions.Select(e => e.Message)) : ex.Message;
    Spectre.Console.AnsiConsole.MarkupLine($"[red]Start-up failed:[/] {Spectre.Console.Markup.Escape(redactor.Redact(message))}");
    Log.Fatal(ex, "Start-up failed");
    return 2;
}
finally
{
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}
