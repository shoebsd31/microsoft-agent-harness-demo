using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.ConsoleApp.Commands;
using ProcurementCopilot.ConsoleApp.Runtime;
using ProcurementCopilot.ConsoleApp.Tui;
using ProcurementCopilot.ConsoleApp.Ui;

namespace ProcurementCopilot.ConsoleApp.Hosting;

/// <summary>Registers the console UX services.</summary>
public static class ConsoleServiceExtensions
{
    /// <summary>Adds the console app, commands and runtime helpers.</summary>
    public static IServiceCollection AddConsoleServices(this IServiceCollection services, LaunchOptions launch)
    {
        services.AddSingleton(launch);
        services.AddSingleton<AppState>();
        services.AddSingleton<AgentBootstrapper>();
        services.AddSingleton<ApprovalPrompt>();
        services.AddSingleton<AgentTurnRunner>();
        services.AddSingleton(sp => new SessionLock(SessionsDirectory(sp)));
        services.AddSingleton(sp => new StatusPublisher(SessionsDirectory(sp)));
        services.AddSingleton<SessionCoordinator>();
        services.AddSingleton(sp => new ObserverPoller(sp.GetRequiredService<StatusPublisher>(), sp.GetRequiredService<AgentBootstrapper>(), SessionsDirectory(sp)));
        services.AddSingleton<SessionDriver>();
        services.AddSingleton<ClassicPresenter>();
        services.AddSingleton<InteractiveConsole>();
        services.AddSingleton<TuiShell>();
        services.AddSingleton<CommandDispatcher>();
        services.AddSingleton<IConsoleCommand, HelpCommand>();
        services.AddSingleton<IConsoleCommand, TodosCommand>();
        services.AddSingleton<IConsoleCommand, ModeCommand>();
        services.AddSingleton<IConsoleCommand, SessionCommand>();
        services.AddSingleton<IConsoleCommand, ContextCommand>();
        services.AddSingleton<IConsoleCommand, ApprovalsCommand>();
        services.AddSingleton<IConsoleCommand, TasksCommand>();
        services.AddSingleton<IConsoleCommand, TracesCommand>();
        services.AddSingleton<IConsoleCommand, DataCommand>();
        services.AddSingleton<IConsoleCommand, TakeoverCommand>();
        services.AddSingleton<IConsoleCommand, DetachCommand>();
        services.AddSingleton<IConsoleCommand, WhoIsCommand>();
        services.AddSingleton<IConsoleCommand, ExitCommand>();
        return services;
    }

    private static string SessionsDirectory(IServiceProvider sp) => sp.GetRequiredService<IOptions<SessionsOptions>>().Value.ResolveDirectory();
}
