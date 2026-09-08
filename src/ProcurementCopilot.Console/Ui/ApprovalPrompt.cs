using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.ConsoleApp.Runtime;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Ui;

/// <summary>
/// Tool approval: the classic prompt shows the tool and pretty-printed arguments and reads y/a/n; the TUI asks through a
/// dialog. Both funnel the choice through <see cref="ResolveAsync"/>, which builds the response and audits the decision.
/// </summary>
public sealed class ApprovalPrompt
{
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly IAuditLog _audit;
    private readonly IClock _clock;

    /// <summary>Initializes the prompt.</summary>
    public ApprovalPrompt(IAuditLog audit, IClock clock)
    {
        _audit = audit;
        _clock = clock;
    }

    /// <summary>Tool name of a request.</summary>
    public static string ToolName(ToolApprovalRequestContent request) =>
        (request.ToolCall as FunctionCallContent)?.Name ?? request.ToolCall?.ToString() ?? "unknown";

    /// <summary>Pretty-printed arguments of a request.</summary>
    public static string Arguments(ToolApprovalRequestContent request) =>
        (request.ToolCall as FunctionCallContent)?.Arguments is { } arguments ? JsonSerializer.Serialize(arguments, PrettyJson) : "{}";

    /// <summary>Classic UI: asks the analyst on the console and returns the response content to send back to the agent.</summary>
    public async Task<AIContent> AskAsync(ToolApprovalRequestContent request, AppState state)
    {
        string name = ToolName(request);
        AnsiConsole.Write(new Panel(new Markup($"[bold yellow]{Markup.Escape(name)}[/]\n[grey]{Markup.Escape(Render.Truncate(Arguments(request), 1200))}[/]"))
        {
            Header = new PanelHeader(" 🔐 Approval required "),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow),
        });

        string choice = Choose();
        AIContent response = await ResolveAsync(request, state, choice).ConfigureAwait(false);
        AnsiConsole.MarkupLine(choice == "n" ? "[red]  ✖ denied[/]" : $"[green]  ✔ {Describe(choice)}[/]");
        return response;
    }

    /// <summary>Applies a choice (<c>y</c> once, <c>a</c> always this session, anything else deny): records the standing approval, audits, builds the response.</summary>
    public async Task<AIContent> ResolveAsync(ToolApprovalRequestContent request, AppState state, string choice)
    {
        string name = ToolName(request);
        AIContent response = choice switch
        {
            "a" => request.CreateAlwaysApproveToolResponse("Analyst granted a standing approval for this tool in this session"),
            "y" => request.CreateResponse(approved: true, reason: "Analyst approved"),
            _ => request.CreateResponse(approved: false, reason: "Analyst denied"),
        };

        if (choice == "a" && !state.StandingApprovals.Contains(name, StringComparer.Ordinal))
        {
            state.StandingApprovals.Add(name);
        }

        string hash = ArgumentHasher.Hash(name, Arguments(request));
        await _audit.RecordApprovalAsync(new ApprovalRecord(_clock.UtcNow, Environment.UserName, name, hash, Describe(choice), SessionIdentity.GetOrCreate(state.Session))).ConfigureAwait(false);
        return response;
    }

    /// <summary>Audit label for a choice.</summary>
    public static string Describe(string choice) => choice switch { "a" => "approve-always", "y" => "approve-once", _ => "deny" };

    private static string Choose()
    {
        while (true)
        {
            AnsiConsole.Markup("[yellow]  [[y]] approve once   [[a]] always approve this tool this session   [[n]] deny  ›[/] ");
            string? input = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (input is "y" or "a" or "n")
            {
                return input;
            }

            if (input is null)
            {
                return "n";
            }
        }
    }
}
