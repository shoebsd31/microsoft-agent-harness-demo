using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace ProcurementCopilot.Agent.Skills;

/// <summary>Creates the file-based skills source rooted at the application base directory, never the current working directory.</summary>
public static class SkillsSourceFactory
{
    /// <summary>Default skills folder next to the executable.</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "skills");

    /// <summary>Creates the source for a skills directory. Scripts are disabled: no runner is supplied.</summary>
    public static AgentFileSkillsSource Create(string? skillsPath = null, ILoggerFactory? loggerFactory = null)
    {
        string path = Path.GetFullPath(skillsPath ?? DefaultPath);
        if (!Path.IsPathRooted(path) || string.Equals(path, Path.GetFullPath(Directory.GetCurrentDirectory()), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Skills must be loaded from an explicit absolute directory, not the current working directory.");
        }

        return new AgentFileSkillsSource(path, scriptRunner: null, options: new AgentFileSkillsSourceOptions { AllowedScriptExtensions = [] }, loggerFactory: loggerFactory);
    }
}
