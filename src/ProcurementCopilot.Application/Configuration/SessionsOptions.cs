namespace ProcurementCopilot.Application.Configuration;

/// <summary>Session persistence settings.</summary>
public sealed class SessionsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Sessions";

    /// <summary>Directory for session files. Empty means <c>%LOCALAPPDATA%/ProcurementCopilot/sessions</c>.</summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>Resolves the effective session directory.</summary>
    public string ResolveDirectory() =>
        string.IsNullOrWhiteSpace(Directory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProcurementCopilot", "sessions")
            : Path.GetFullPath(Directory);
}
