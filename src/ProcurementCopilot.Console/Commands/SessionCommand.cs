using Microsoft.Agents.AI;
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.ConsoleApp.Ui;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Commands;

/// <summary><c>/session new | list | resume &lt;id&gt;</c>.</summary>
public sealed class SessionCommand : IConsoleCommand
{
    private readonly ISessionStore _store;
    private readonly AgentBootstrapper _bootstrapper;

    /// <summary>Initializes the command.</summary>
    public SessionCommand(ISessionStore store, AgentBootstrapper bootstrapper)
    {
        _store = store;
        _bootstrapper = bootstrapper;
    }

    /// <inheritdoc />
    public string Name => "/session";

    /// <inheritdoc />
    public string Help => "/session new | list | resume <id> - manage persisted sessions";

    /// <inheritdoc />
    public async Task ExecuteAsync(string[] args, AppState state)
    {
        switch (args.FirstOrDefault()?.ToLowerInvariant())
        {
            case "new":
                await SessionPersistence.SaveAsync(state.Agent, state.Session, _store, null).ConfigureAwait(false);
                state.Session = await state.Agent.CreateSessionAsync().ConfigureAwait(false);
                Render.Info("New session " + SessionIdentity.GetOrCreate(state.Session).ToString("N"));
                break;
            case "list":
                await ListAsync().ConfigureAwait(false);
                break;
            case "resume" when args.Length > 1 && Guid.TryParse(args[1], out Guid id):
                await ResumeAsync(state, id).ConfigureAwait(false);
                break;
            default:
                Render.Info("Current session: " + SessionIdentity.GetOrCreate(state.Session).ToString("N") + ". Usage: /session new | list | resume <id>");
                break;
        }
    }

    private async Task ListAsync()
    {
        IReadOnlyList<SessionSummary> sessions = await _store.ListAsync().ConfigureAwait(false);
        if (sessions.Count == 0)
        {
            Render.Info("No stored sessions.");
            return;
        }

        var table = new Table().Border(TableBorder.Simple).AddColumn("Id").AddColumn("Updated (UTC)").AddColumn("Title");
        foreach (SessionSummary s in sessions.Take(20))
        {
            table.AddRow(s.Id.ToString("N"), s.UpdatedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), Markup.Escape(s.Title));
        }

        AnsiConsole.Write(table);
    }

    private async Task ResumeAsync(AppState state, Guid id)
    {
        AgentSession? resumed = await _bootstrapper.TryResumeAsync(state.Agent, id).ConfigureAwait(false);
        if (resumed is null)
        {
            Render.Warn("Session not found: " + id.ToString("N"));
            return;
        }

        state.Session = resumed;
        int messages = state.Factory.HistoryProvider?.GetMessages(resumed).Count ?? 0;
        Render.Info($"Resumed session {id:N} with {messages} stored messages (history up to the last completed model call).");
        if (state.Todos is { } todos)
        {
            Render.Todos(await todos.GetAllTodosAsync(resumed).ConfigureAwait(false));
        }
    }
}
