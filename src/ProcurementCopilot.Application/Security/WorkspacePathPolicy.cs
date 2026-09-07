using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Application.Security;

/// <summary>Kind of access requested on a workspace path.</summary>
public enum PathAccess
{
    /// <summary>Read the file or list the directory.</summary>
    Read,

    /// <summary>Create, overwrite or delete.</summary>
    Write,
}

/// <summary>
/// The single filesystem confinement rule used by file access, the shell and the outbox writers.
/// Canonicalises with <see cref="Path.GetFullPath(string)"/> and compares against the root with a trailing separator.
/// </summary>
public sealed class WorkspacePathPolicy
{
    private readonly WorkspaceOptions _options;
    private readonly string[] _readOnly;
    private readonly string[] _writable;

    /// <summary>Initializes the policy for an absolute workspace root.</summary>
    public WorkspacePathPolicy(string workspaceRoot, WorkspaceOptions options)
    {
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot));
        _options = options;
        _readOnly = options.EffectiveReadOnlyPaths.Select(Sub).ToArray();
        _writable = options.EffectiveWritablePaths.Select(Sub).ToArray();
    }

    /// <summary>Gets the canonical root without a trailing separator.</summary>
    public string Root { get; }

    /// <summary>Resolves a workspace-relative path and checks the requested access.</summary>
    public Result<string> Resolve(string? relativePath, PathAccess access)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Error.Validation("Path.Empty", "A path is required.");
        }

        if (relativePath.Contains('\0', StringComparison.Ordinal))
        {
            return Error.Validation("Path.Invalid", "The path contains invalid characters.");
        }

        if (relativePath.StartsWith(@"\\", StringComparison.Ordinal) || relativePath.StartsWith("//", StringComparison.Ordinal))
        {
            return Error.Validation("Path.Unc", "UNC paths are not allowed.");
        }

        if (Path.IsPathRooted(relativePath) || (relativePath.Length > 1 && relativePath[1] == ':'))
        {
            return Error.Validation("Path.Absolute", "Absolute paths are not allowed; use a path relative to the workspace.");
        }

        if (relativePath.Split('/', '\\').Any(segment => segment == ".."))
        {
            return Error.Validation("Path.Traversal", "Parent directory segments ('..') are not allowed.");
        }

        string full = Path.GetFullPath(Path.Combine(Root, relativePath));
        if (!IsUnder(full, Root))
        {
            return Error.Validation("Path.OutsideRoot", "The path resolves outside the workspace.");
        }

        if (IsReparsePoint(full))
        {
            return Error.Validation("Path.Symlink", "Symbolic links and junctions are not allowed.");
        }

        bool isRoot = string.Equals(full, Root, StringComparison.OrdinalIgnoreCase);
        if (access == PathAccess.Write)
        {
            if (!_writable.Any(w => IsUnder(full, w)))
            {
                return Error.Validation("Path.ReadOnly", "Writing is only allowed under: " + string.Join(", ", _options.EffectiveWritablePaths));
            }

            Result<string> ext = CheckExtension(full);
            if (ext.IsFailure)
            {
                return ext.Error;
            }
        }
        else if (!isRoot && !_readOnly.Concat(_writable).Any(p => IsUnder(full, p)))
        {
            return Error.Validation("Path.NotShared", "Only the shared folders are readable: " + string.Join(", ", _options.EffectiveReadOnlyPaths.Concat(_options.EffectiveWritablePaths)));
        }

        return full;
    }

    /// <summary>Checks that a file's extension is on the allowlist (directories pass).</summary>
    public Result<string> CheckExtension(string fullPath)
    {
        string extension = Path.GetExtension(fullPath);
        if (extension.Length == 0 && Directory.Exists(fullPath))
        {
            return fullPath;
        }

        return _options.EffectiveAllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            ? fullPath
            : Error.Validation("Path.Extension", "Only these extensions are allowed: " + string.Join(", ", _options.EffectiveAllowedExtensions));
    }

    /// <summary>Checks a byte count against <see cref="WorkspaceOptions.MaxFileBytes"/>.</summary>
    public Result<long> CheckSize(long bytes) =>
        bytes <= _options.MaxFileBytes ? bytes : Error.Validation("Path.TooLarge", $"Files larger than {_options.MaxFileBytes} bytes are not allowed.");

    /// <summary>Returns <see langword="true"/> when <paramref name="fullPath"/> equals or is below <paramref name="root"/>.</summary>
    public static bool IsUnder(string fullPath, string root)
    {
        string trimmedRoot = Path.TrimEndingDirectorySeparator(root);
        return string.Equals(fullPath, trimmedRoot, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(trimmedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(trimmedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private string Sub(string relative) => Path.GetFullPath(Path.Combine(Root, relative));

    private static bool IsReparsePoint(string full)
    {
        for (string? current = full; current is not null && current.Length > 0; current = Path.GetDirectoryName(current))
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                return File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint) && !string.Equals(current, Path.GetPathRoot(current), StringComparison.Ordinal);
            }
        }

        return false;
    }
}
