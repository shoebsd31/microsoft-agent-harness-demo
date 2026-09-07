namespace ProcurementCopilot.Infrastructure.Data;

/// <summary>Finds the <c>data/</c> directory next to the application, falling back to the repository root during development.</summary>
public static class DataFileLocator
{
    /// <summary>Returns the first directory containing <c>rfps.json</c>, searching the base directory and its ancestors.</summary>
    public static string Locate(string? baseDirectory = null)
    {
        string start = baseDirectory ?? AppContext.BaseDirectory;
        for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "rfps.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"Could not find a 'data' directory with rfps.json starting from '{start}'.");
    }
}
