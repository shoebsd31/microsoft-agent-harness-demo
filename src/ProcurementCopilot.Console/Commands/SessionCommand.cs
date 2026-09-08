using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.ConsoleApp.Ui;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Commands;

/// <summary><c>/session new | list | resume &lt;id&gt; | attach &lt;id&gt;</c>.</summary>
public sealed class SessionCommand : IConsoleCommand
{
    private readonly ISessionStore _store;
    private readonly SessionCoordinator _coordinator;

    /// <summary>Initializes the command.</summary>
    public SessionCommand(ISessionStore store, SessionCoordinator coordinator)
    {
        _store = store;
        _coordinator = coordinator;
    }

    /// <inheritdoc />
    public string Name => "/session";

    /// <inheritdoc />
    public string Help => "/session new | list | resume <id> | attach <id> - manage persisted sessions (resume drives, attach observes)";

    /// <inheritdoc />
    public async Task ExecuteAsync(string[] args, AppState state)
    {
        switch (args.FirstOrDefault()?.ToLowerInvariant())
        {
            case "new":
                await NewAsync(state).ConfigureAwait(false);
                break;
            case "list":
                await ListAsync().ConfigureAwait(false);
                break;
            case "resume" when args.Length > 1 && Guid.TryParse(args[1], out Guid id):
                await ResumeAsync(state, id).ConfigureAwait(false);
                break;
            case "attach" when args.Length > 1 && Guid.TryParse(args[1], out Guid observed):
                await AttachAsync(state, observed).ConfigureAwait(false);
                break;
            default:
                Render.Info($"Current session: {SessionIdentity.GetOrCreate(state.Session):N} ({state.Role}). Usage: /session new | list | resume <id> | attach <id>");
                break;
        }
    }

    private async Task NewAsync(AppState state)
    {
        await _coordinator.SaveAsync(state, null).ConfigureAwait(false);
        state.Session = await state.Agent.CreateSessionAsync().ConfigureAwait(false);
        state.Tasks.Tasks.Clear();
        await _coordinator.TryDriveAsync(state).ConfigureAwait(false);
        Render.Info("New session " + SessionIdentity.GetOrCreate(state.Session).ToString("N"));
    }

    private async Task ListAsync()
    {
        IReadOnlyList<SessionSummary> sessions = await _store.ListAsync().ConfigureAwait(false);
        if (sessions.Count == 0)
        {
            Render.Info("No stored sessions.");
            return;
        }

        var table = new Table().Border(TableBorder.Simple).AddColumn("Id").AddColumn("Updated (UTC)").AddColumn("Driver").AddColumn("Title");
        foreach (SessionSummary s in sessions.Take(20))
        {
            SessionLockInfo? owner = _coordinator.LiveOwner(s.Id);
            table.AddRow(s.Id.ToString("N"), s.UpdatedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                owner is null ? "[grey]free[/]" : owner.Pid == Environment.ProcessId ? "[green]this instance[/]" : Markup.Escape(owner.Instance), Markup.Escape(s.Title));
        }

        AnsiConsole.Write(table);
    }

    private async Task ResumeAsync(AppState state, Guid id)
    {
        await _coordinator.SaveAsync(state, null).ConfigureAwait(false);
        if (!await _coordinator.AttachAsync(state, id).ConfigureAwait(false))
        {
            Render.Warn("Session not found: " + id.ToString("N"));
            return;
        }

        state.Tasks.Tasks.Clear();
        SessionLockInfo? owner = await _coordinator.TryDriveAsync(state).ConfigureAwait(false);
        int messages = state.Factory.HistoryProvider?.GetMessages(state.Session).Count ?? 0;
        if (owner is not null)
        {
            Render.Warn($"Session {id:N} is driven by {owner.Instance}; attached as an observer ({messages} stored messages). /takeover once it is free.");
            return;
        }

        Render.Info($"Resumed session {id:N} with {messages} stored messages (history up to the last completed model call).");
        if (state.Todos is { } todos)
        {
            Render.Todos(await todos.GetAllTodosAsync(state.Session).ConfigureAwait(false));
        }
    }

    private async Task AttachAsync(AppState state, Guid id)
    {
        await _coordinator.DetachIfDrivingAsync(state).ConfigureAwait(false);
        if (!await _coordinator.AttachAsync(state, id).ConfigureAwait(false))
        {
            Render.Warn("Session not found: " + id.ToString("N"));
            return;
        }

        SessionLockInfo? owner = _coordinator.LiveOwner(id);
        Render.Info(owner is null ? $"Observing session {id:N} (nobody drives it; /takeover to drive)." : $"Observing session {id:N}, driven by {owner.Instance}.");
    }
}
