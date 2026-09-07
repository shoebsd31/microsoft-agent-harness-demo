using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProcurementCopilot.Agent;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Testing;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Hosting;

/// <summary><c>--self-check</c>: resolves every service, loads the seed data, composes the harness with a fake client and exits 0. No model call.</summary>
public static class SelfCheck
{
    /// <summary>Runs the check and returns the process exit code.</summary>
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Check").AddColumn("Result");
        try
        {
            FoundryOptions foundry = services.GetRequiredService<IOptions<FoundryOptions>>().Value;
            table.AddRow("Configuration bound and validated", "[green]ok[/]");
            table.AddRow("Foundry credentials", foundry.IsConfigured ? "[green]configured[/]" : "[yellow]not configured[/] (missing: " + Markup.Escape(string.Join(", ", foundry.MissingKeys())) + ") - live mode unavailable, --fake works");

            int rfps = (await services.GetRequiredService<IRfpRepository>().GetAllAsync().ConfigureAwait(false)).Count;
            int vendors = (await services.GetRequiredService<IVendorRepository>().GetAllAsync().ConfigureAwait(false)).Count;
            table.AddRow("Seed data", $"[green]{rfps} RFPs, {vendors} vendors[/]");

            WorkspacePathPolicy paths = services.GetRequiredService<WorkspacePathPolicy>();
            table.AddRow("Workspace root", Markup.Escape(paths.Root));

            HarnessAgentFactory factory = services.GetRequiredService<HarnessAgentFactory>();
            ProcurementAgentOptions options = services.GetRequiredService<ProcurementAgentOptions>();
            using var fake = new ScriptedChatClient();
            HarnessAgent agent = factory.Create(fake, options);
            AgentSession session = await agent.CreateSessionAsync().ConfigureAwait(false);
            string mode = await agent.GetService<AgentModeProvider>()!.GetModeAsync(session).ConfigureAwait(false);
            HarnessAgentOptions built = factory.BuildOptions(fake, options);
            table.AddRow("Harness composition", $"[green]{built.ChatOptions!.Tools!.Count} tools, {built.BackgroundAgents!.Count()} background agents, {built.LoopEvaluators!.Count()} loop evaluators, mode '{mode}'[/]");
            table.AddRow("Skills directory", Markup.Escape(options.SkillsPath) + (Directory.Exists(options.SkillsPath) ? " [green](found)[/]" : " [red](missing)[/]"));
            table.AddRow("Approval policy", Markup.Escape(string.Join(", ", options.Security.ApprovalPolicy.EffectiveRequireApprovalFor)));
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[green]Self-check passed.[/]");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or OptionsValidationException)
        {
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[red]Self-check failed:[/] {Markup.Escape(services.GetRequiredService<SecretRedactor>().Redact(ex.Message))}");
            return 1;
        }
    }
}
