using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.ConsoleApp.Ui;

namespace ProcurementCopilot.ConsoleApp.Commands;

/// <summary>A slash command.</summary>
public interface IConsoleCommand
{
    /// <summary>The command name including the slash, for example <c>/todos</c>.</summary>
    string Name { get; }

    /// <summary>One-line help.</summary>
    string Help { get; }

    /// <summary>Executes the command with its arguments.</summary>
    Task ExecuteAsync(string[] args, AppState state);
}

/// <summary>Routes <c>/commands</c> to their handlers.</summary>
public sealed class CommandDispatcher
{
    private readonly IReadOnlyList<IConsoleCommand> _commands;

    /// <summary>Initializes the dispatcher.</summary>
    public CommandDispatcher(IEnumerable<IConsoleCommand> commands) => _commands = commands.ToList();

    /// <summary>All registered commands.</summary>
    public IReadOnlyList<IConsoleCommand> Commands => _commands;

    /// <summary>Dispatches a line that starts with a slash.</summary>
    public async Task DispatchAsync(string line, AppState state)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IConsoleCommand? command = _commands.FirstOrDefault(c => string.Equals(c.Name, parts[0], StringComparison.OrdinalIgnoreCase));
        if (command is null)
        {
            Render.Warn($"Unknown command {parts[0]}. Type /help.");
            return;
        }

        try
        {
            await command.ExecuteAsync(parts.Skip(1).ToArray(), state).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            Render.Error(ex.Message);
        }
    }
}
