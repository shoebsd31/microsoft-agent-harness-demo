using System.Runtime.InteropServices;

namespace ProcurementCopilot.Agent.Shell;

/// <summary>The shell chosen for the current OS.</summary>
/// <param name="Family"><c>posix</c> or <c>powershell</c>.</param>
/// <param name="Argv">Explicit shell argv for the executor, or <see langword="null"/> for the package default.</param>
public sealed record ShellSelection(string Family, IReadOnlyList<string>? Argv);

/// <summary>Picks <c>/bin/sh</c> on Unix; on Windows prefers Git Bash when present so the POSIX allowlist works, else PowerShell/cmd.</summary>
public static class ShellSelector
{
    /// <summary>Resolves the shell for this machine.</summary>
    public static ShellSelection Resolve()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ShellSelection("posix", File.Exists("/bin/sh") ? ["/bin/sh"] : null);
        }

        string? bash = FindOnPath("bash.exe");
        return bash is null ? new ShellSelection("powershell", null) : new ShellSelection("posix", [bash]);
    }

    private static string? FindOnPath(string fileName)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path is null)
        {
            return null;
        }

        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir.Trim(), fileName))
            .Where(candidate => !candidate.Contains(@"\System32\", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(File.Exists);
    }
}
