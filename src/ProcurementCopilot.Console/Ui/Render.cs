using Microsoft.Agents.AI;
using ProcurementCopilot.Application.Abstractions;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Ui;

/// <summary>Spectre.Console rendering helpers. Keeps all markup in one place.</summary>
public static class Render
{
    /// <summary>Prints the start-up banner with the demo scenario and the suggested first prompt.</summary>
    public static void Banner(bool fakeMode, Guid sessionId)
    {
        AnsiConsole.Write(new FigletText("Procurement Copilot").Color(Color.Teal));
        var panel = new Panel(new Markup(
            "[bold]Contoso Industrial Systems[/] - harness demo on Microsoft Agent Framework\n" +
            "Scenario: evaluate [teal]RFP-2026-017[/] (40 CNC vertical machining centres, EUR) with 5 vendor bids: one sanctioned vendor, one missing ISO 14001, one USD bid, one ambiguous delivery clause.\n\n" +
            "Suggested first prompt: [green]\"Evaluate RFP-2026-017 and recommend a vendor.\"[/]\n" +
            (fakeMode ? "[yellow]FAKE MODE[/]: replaying a canned run with the scripted client (no model calls). After the plan, type [green]/mode execute[/] then [green]go[/].\n" : string.Empty) +
            $"Session [grey]{sessionId:N}[/]. Type [green]/help[/] for commands. Ctrl+C cancels the current run; Ctrl+C again exits."))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader(" Welcome "),
        };
        AnsiConsole.Write(panel);
    }

    /// <summary>Prints the prompt with the mode badge.</summary>
    public static void Prompt(string mode) =>
        AnsiConsole.Markup($"[{ModeColor(mode)} bold] {mode.ToUpperInvariant()} [/] [teal]>[/] ");

    /// <summary>Returns the colour used for a mode badge.</summary>
    public static string ModeColor(string mode) => string.Equals(mode, AgentModes.Execute, StringComparison.OrdinalIgnoreCase) ? "black on green" : "black on aqua";

    /// <summary>Prints an informational line.</summary>
    public static void Info(string text) => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(text)}[/]");

    /// <summary>Prints a warning line.</summary>
    public static void Warn(string text) => AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(text)}[/]");

    /// <summary>Prints a friendly error line (details go to the log file, never the screen).</summary>
    public static void Error(string text) => AnsiConsole.MarkupLine($"[red]✖ {Markup.Escape(text)}[/]");

    /// <summary>Prints a collapsed tool-call line such as <c>▸ score_bid(RFP-2026-017, BID-003) → 89.60</c>.</summary>
    public static void ToolCall(string name, string arguments, string result) =>
        AnsiConsole.MarkupLine($"  [grey]▸[/] [teal]{Markup.Escape(name)}[/][grey]({Markup.Escape(arguments)})[/] [grey]→[/] {Markup.Escape(result)}");

    /// <summary>Prints a loop feedback line.</summary>
    public static void LoopFeedback(string text) =>
        AnsiConsole.MarkupLine($"  [grey italic]↻ loop: {Markup.Escape(Truncate(text, 200))}[/]");

    /// <summary>Renders the todo side panel.</summary>
    public static void Todos(IReadOnlyList<TodoItem> todos)
    {
        if (todos.Count == 0)
        {
            Info("No todos yet.");
            return;
        }

        var table = new Table().Border(TableBorder.Simple).AddColumn(string.Empty).AddColumn("#").AddColumn("Todo");
        foreach (TodoItem item in todos)
        {
            table.AddRow(item.IsComplete ? "[green]✓[/]" : "[grey]○[/]", item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                item.IsComplete ? $"[grey strikethrough]{Markup.Escape(item.Title)}[/]" : Markup.Escape(item.Title));
        }

        AnsiConsole.Write(new Panel(table) { Header = new PanelHeader($" Todos {todos.Count(t => t.IsComplete)}/{todos.Count} "), Border = BoxBorder.Rounded });
    }

    /// <summary>Truncates text for one-line display.</summary>
    public static string Truncate(string text, int max)
    {
        string flat = text.Replace("\r", string.Empty, StringComparison.Ordinal).Replace('\n', ' ').Trim();
        return flat.Length <= max ? flat : flat[..max] + "…";
    }
}
