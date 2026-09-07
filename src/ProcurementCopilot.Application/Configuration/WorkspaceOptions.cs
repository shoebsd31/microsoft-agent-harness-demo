using System.ComponentModel.DataAnnotations;

namespace ProcurementCopilot.Application.Configuration;

/// <summary>Filesystem confinement settings for the agent workspace.</summary>
public sealed class WorkspaceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Workspace";

    /// <summary>Workspace root, relative to the application base directory or absolute.</summary>
    [Required]
    public string Root { get; set; } = "workspace";

    /// <summary>Sub-paths (relative to the root) the agent may only read. Bound from configuration; defaults to <c>rfps</c>.</summary>
    public IReadOnlyList<string> ReadOnlyPaths { get; set; } = [];

    /// <summary>Sub-paths (relative to the root) the agent may read and write. Bound from configuration; defaults to <c>output</c>.</summary>
    public IReadOnlyList<string> WritablePaths { get; set; } = [];

    /// <summary>Gets the effective read-only paths.</summary>
    public IReadOnlyList<string> EffectiveReadOnlyPaths => ReadOnlyPaths.Count > 0 ? ReadOnlyPaths : ["rfps"];

    /// <summary>Gets the effective writable paths.</summary>
    public IReadOnlyList<string> EffectiveWritablePaths => WritablePaths.Count > 0 ? WritablePaths : ["output"];

    /// <summary>Maximum file size the agent may read or write.</summary>
    [Range(1, 100_000_000)]
    public long MaxFileBytes { get; set; } = 1_048_576;

    /// <summary>File extensions the agent may touch. Bound from configuration.</summary>
    public IReadOnlyList<string> AllowedExtensions { get; set; } = [];

    /// <summary>Gets the effective extension allowlist.</summary>
    public IReadOnlyList<string> EffectiveAllowedExtensions => AllowedExtensions.Count > 0 ? AllowedExtensions : [".md", ".txt", ".json", ".csv", ".jsonl"];

    /// <summary>Resolves the absolute workspace root against a base directory.</summary>
    public string ResolveRoot(string baseDirectory) =>
        Path.GetFullPath(Path.IsPathRooted(Root) ? Root : Path.Combine(baseDirectory, Root));
}
