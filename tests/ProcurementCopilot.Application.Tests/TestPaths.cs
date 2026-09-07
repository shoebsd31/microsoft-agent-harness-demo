using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Application.Security;

namespace ProcurementCopilot.Application.Tests;

/// <summary>Creates isolated temporary workspaces for policy tests.</summary>
public static class TestPaths
{
    public static string NewWorkspace()
    {
        string root = Path.Combine(Path.GetTempPath(), "pc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "rfps", "RFP-2026-017"));
        Directory.CreateDirectory(Path.Combine(root, "output"));
        File.WriteAllText(Path.Combine(root, "rfps", "RFP-2026-017", "rfp.md"), "# rfp");
        return root;
    }

    public static WorkspacePathPolicy Policy(string? root = null, WorkspaceOptions? options = null) =>
        new(root ?? NewWorkspace(), options ?? new WorkspaceOptions());

    public static string DataDirectory()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "rfps.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("data directory not found");
    }
}
