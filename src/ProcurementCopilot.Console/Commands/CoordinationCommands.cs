using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.ConsoleApp.Ui;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Commands;

/// <summary><c>/takeover</c>: an observer becomes the driver when the lock is free.</summary>
public sealed class TakeoverCommand(SessionCoordinator coordinator) : IConsoleCommand
{
    /// <inheritdoc />
    public string Name => "/takeover";

    /// <inheritdoc />
    public string Help => "observer only - drive this session from here once the current driver has exited or detached";

    /// <inheritdoc />
    public async Task ExecuteAsync(string[] args, AppState state)
    {
        if (state.Role == SessionRole.Driver)
        {
            Render.Info("This instance already drives the session.");
            return;
        }

        SessionLockInfo? owner = await coordinator.TakeoverAsync(state).ConfigureAwait(false);
        if (owner is not null)
        {
            Render.Warn($"Still driven by {owner.Instance} (heartbeat {owner.HeartbeatAt.ToLocalTime():HH:mm:ss}). Ask that instance to /detach or /exit first.");
            return;
        }

        int messages = state.Factory.HistoryProvider?.GetMessages(state.Session).Count ?? 0;
        AnsiConsole.MarkupLine($"[green]Now driving[/] session {SessionIdentity.GetOrCreate(state.Session):N} with {messages} stored messages. Mode: {await state.GetModeAsync().ConfigureAwait(false)}.");
    }
}

/// <summary><c>/detach</c>: the driver saves, releases the lock and keeps watching as an observer.</summary>
public sealed class DetachCommand(SessionCoordinator coordinator) : IConsoleCommand
{
    /// <inheritdoc />
    public string Name => "/detach";

    /// <inheritdoc />
    public string Help => "driver only - save, release the session lock and keep watching as an observer";

    /// <inheritdoc />
    public async Task ExecuteAsync(string[] args, AppState state)
    {
        if (state.Role == SessionRole.Observer)
        {
            Render.Info("This instance is already an observer.");
            return;
        }

        await coordinator.DetachAsync(state).ConfigureAwait(false);
        Render.Info("Detached. The session is saved and unlocked; another instance can /takeover. Type /takeover here to drive it again.");
    }
}

/// <summary><c>/whois</c>: who drives the session and what the status file says.</summary>
public sealed class WhoIsCommand(SessionCoordinator coordinator, StatusPublisher status) : IConsoleCommand
{
    /// <inheritdoc />
    public string Name => "/whois";

    /// <inheritdoc />
    public string Help => "show which instance drives this session and its last published activity";

    /// <inheritdoc />
    public Task ExecuteAsync(string[] args, AppState state)
    {
        Guid id = SessionIdentity.GetOrCreate(state.Session);
        SessionLockInfo? owner = coordinator.LiveOwner(id);
        SessionStatus? published = status.Read(id);
        var table = new Table().Border(TableBorder.Simple).AddColumn("Item").AddColumn("Value");
        table.AddRow("Session", id.ToString("N"));
        table.AddRow("This instance", $"{SessionLock.InstanceName} ({state.Role})");
        table.AddRow("Driver", owner is null ? "[grey]none (free)[/]" : Markup.Escape($"{owner.Instance}, since {owner.AcquiredAt.ToLocalTime():HH:mm:ss}, heartbeat {owner.HeartbeatAt.ToLocalTime():HH:mm:ss}"));
        table.AddRow("Published activity", published is null ? "[grey]no status file yet[/]" : Markup.Escape($"{published.Activity}{(published.CurrentTool is null ? string.Empty : " (" + published.CurrentTool + ")")}, mode {published.Mode}, updated {published.UpdatedAt.ToLocalTime():HH:mm:ss}"));
        table.AddRow("Last prompt", Markup.Escape(published?.LastPrompt ?? "-"));
        AnsiConsole.Write(table);
        return Task.CompletedTask;
    }
}
