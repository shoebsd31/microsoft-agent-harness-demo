using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.ConsoleApp.Runtime;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Ui;

/// <summary>Interactive tool approval: shows the tool and pretty-printed arguments, offers once / always / deny, audits the decision.</summary>
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

    /// <summary>Asks the analyst and returns the response content to send back to the agent.</summary>
    public async Task<AIContent> AskAsync(ToolApprovalRequestContent request, AppState state)
    {
        var call = request.ToolCall as FunctionCallContent;
        string name = call?.Name ?? request.ToolCall?.ToString() ?? "unknown";
        string arguments = call?.Arguments is null ? "{}" : JsonSerializer.Serialize(call.Arguments, PrettyJson);

        AnsiConsole.Write(new Panel(new Markup($"[bold yellow]{Markup.Escape(name)}[/]\n[grey]{Markup.Escape(Render.Truncate(arguments, 1200))}[/]"))
        {
            Header = new PanelHeader(" 🔐 Approval required "),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow),
        });

        string choice = Choose();
        string decision = choice switch { "a" => "approve-always", "y" => "approve-once", _ => "deny" };
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

        string hash = ArgumentHasher.Hash(name, arguments);
        await _audit.RecordApprovalAsync(new ApprovalRecord(_clock.UtcNow, Environment.UserName, name, hash, decision, SessionIdentity.GetOrCreate(state.Session))).ConfigureAwait(false);
        AnsiConsole.MarkupLine(decision == "deny" ? "[red]  ✖ denied[/]" : $"[green]  ✔ {decision}[/]");
        return response;
    }

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
