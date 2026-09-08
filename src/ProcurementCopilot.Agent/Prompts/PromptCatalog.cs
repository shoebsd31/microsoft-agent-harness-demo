using System.Reflection;

namespace ProcurementCopilot.Agent.Prompts;

/// <summary>Loads the prompt markdown files embedded in this assembly.</summary>
public sealed class PromptCatalog
{
    private const string Prefix = "Prompts.";

    private const string OrganisationPlaceholder = "{{Organisation}}";
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);

    /// <summary>Organisation name substituted for the organisation placeholder in the prompts.</summary>
    public string Organisation { get; set; } = "Contoso Industrial Systems";

    /// <summary>Harness-level procurement guidance appended to <c>HarnessAgent.DefaultInstructions</c>.</summary>
    public string HarnessAddendum => Render(Load("harness-instructions.md"));

    /// <summary>Agent-level persona and output-style instructions.</summary>
    public string AgentInstructions => Render(Load("agent-instructions.md"));

    /// <summary>Replaces the organisation placeholder.</summary>
    public string Render(string text) => text.Replace(OrganisationPlaceholder, Organisation, StringComparison.Ordinal);

    /// <summary>Instructions for the market-research background agent.</summary>
    public string MarketResearchInstructions => Load("market-research-instructions.md");

    /// <summary>Instructions for the risk-analyst background agent.</summary>
    public string RiskAnalystInstructions => Load("risk-analyst-instructions.md");

    /// <summary>Returns the names of all embedded prompt resources.</summary>
    public static IReadOnlyList<string> ResourceNames() =>
        typeof(PromptCatalog).Assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)).ToList();

    /// <summary>Loads an embedded prompt by file name.</summary>
    public string Load(string fileName)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(fileName, out string? cached))
            {
                return cached;
            }

            Assembly assembly = typeof(PromptCatalog).Assembly;
            using Stream stream = assembly.GetManifestResourceStream(Prefix + fileName)
                ?? throw new FileNotFoundException($"Embedded prompt '{fileName}' was not found.");
            using var reader = new StreamReader(stream);
            string text = reader.ReadToEnd().Trim();
            _cache[fileName] = text;
            return text;
        }
    }
}
