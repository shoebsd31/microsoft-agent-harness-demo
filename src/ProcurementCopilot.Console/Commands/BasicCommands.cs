using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.ConsoleApp.Ui;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Commands;

/// <summary><c>/help</c>. Resolves the command list lazily to avoid a dependency cycle on itself.</summary>
public sealed class HelpCommand(IServiceProvider services) : IConsoleCommand
{
    /// <inheritdoc />
    public string Name => "/help";

    /// <inheritdoc />
    public string Help => "list commands";

    /// <inheritdoc />
    public Task ExecuteAsync(string[] args, AppState state)
    {
        var table = new Table().Border(TableBorder.Simple).AddColumn("Command").AddColumn("What it does");
        foreach (IConsoleCommand command in services.GetServices<IConsoleCommand>().OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            table.AddRow(Markup.Escape(command.Name), Markup.Escape(command.Help));
        }

        AnsiConsole.Write(table);
        return Task.CompletedTask;
    }
}

/// <summary><c>/todos</c>.</summary>
public sealed class TodosCommand : IConsoleCommand
{
    /// <inheritdoc />
    public string Name => "/todos";

    /// <inheritdoc />
    public string Help => "show the todo list";

    /// <inheritdoc />
    public async Task ExecuteAsync(string[] args, AppState state)
    {
        if (state.Todos is null)
        {
            Render.Warn("Todo provider is not available.");
            return;
        }

        Render.Todos(await state.Todos.GetAllTodosAsync(state.Session).ConfigureAwait(false));
    }
}

/// <summary><c>/mode [plan|execute]</c>.</summary>
public sealed class ModeCommand(SessionCoordinator coordinator) : IConsoleCommand
{
    /// <inheritdoc />
    public string Name => "/mode";

    /// <inheritdoc />
    public string Help => "/mode [plan|execute] - show or switch the agent mode (side effects only in execute)";

    /// <inheritdoc />
    public async Task ExecuteAsync(string[] args, AppState state)
    {
        AgentModeProvider? modes = state.Modes;
        if (modes is null)
        {
            Render.Warn("Mode provider is not available.");
            return;
        }

        if (args.Length == 0)
        {
            Render.Info("Current mode: " + await modes.GetModeAsync(state.Session).ConfigureAwait(false));
            return;
        }

        string requested = args[0].ToLowerInvariant();
        if (requested is not (AgentModes.Plan or AgentModes.Execute))
        {
            Render.Warn("Modes: plan, execute.");
            return;
        }

        if (state.Role == SessionRole.Observer)
        {
            Render.Warn("Observers cannot change the mode; /takeover first.");
            return;
        }

        await modes.SetModeAsync(state.Session, requested).ConfigureAwait(false);
        coordinator.PublishMode(requested);
        AnsiConsole.MarkupLine($"Switched to [{Render.ModeColor(requested)}] {requested.ToUpperInvariant()} [/] mode.");
    }
}

/// <summary><c>/exit</c>.</summary>
public sealed class ExitCommand : IConsoleCommand
{
    /// <inheritdoc />
    public string Name => "/exit";

    /// <inheritdoc />
    public string Help => "save the session and quit";

    /// <inheritdoc />
    public Task ExecuteAsync(string[] args, AppState state)
    {
        state.ExitRequested = true;
        return Task.CompletedTask;
    }
}
