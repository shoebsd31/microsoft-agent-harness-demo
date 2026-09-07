using Microsoft.Extensions.DependencyInjection;
using ProcurementCopilot.ConsoleApp.Commands;
using ProcurementCopilot.ConsoleApp.Runtime;
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
        services.AddSingleton<InteractiveConsole>();
        services.AddSingleton<CommandDispatcher>();
        services.AddSingleton<IConsoleCommand, HelpCommand>();
        services.AddSingleton<IConsoleCommand, TodosCommand>();
        services.AddSingleton<IConsoleCommand, ModeCommand>();
        services.AddSingleton<IConsoleCommand, SessionCommand>();
        services.AddSingleton<IConsoleCommand, ContextCommand>();
        services.AddSingleton<IConsoleCommand, ApprovalsCommand>();
        services.AddSingleton<IConsoleCommand, TasksCommand>();
        services.AddSingleton<IConsoleCommand, TracesCommand>();
        services.AddSingleton<IConsoleCommand, ExitCommand>();
        return services;
    }
}
