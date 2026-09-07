using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent.Prompts;
using ProcurementCopilot.Agent.Tools;
using ProcurementCopilot.Application.Configuration;

namespace ProcurementCopilot.Agent.Background;

/// <summary>A child agent's narrow contract: name, instructions and the only tools it may use.</summary>
/// <param name="Name">Unique name used by <c>background_agents_start_task</c>.</param>
/// <param name="Description">What the agent is for.</param>
/// <param name="Instructions">System instructions.</param>
/// <param name="Tools">Tools; never side-effecting, file or shell tools.</param>
public sealed record BackgroundAgentDefinition(string Name, string Description, string Instructions, IReadOnlyList<AITool> Tools);

/// <summary>Builds the two child agents (plain <see cref="ChatClientAgent"/>s) with narrow tool sets, a timeout and a shared concurrency cap.</summary>
public sealed class BackgroundAgentFactory
{
    /// <summary>Name of the market-research child agent.</summary>
    public const string MarketResearch = "market-research";

    /// <summary>Name of the risk-analyst child agent.</summary>
    public const string RiskAnalyst = "risk-analyst";

    private readonly ProcurementToolset _toolset;
    private readonly PromptCatalog _prompts;

    /// <summary>Initializes the factory.</summary>
    public BackgroundAgentFactory(ProcurementToolset toolset, PromptCatalog prompts)
    {
        _toolset = toolset;
        _prompts = prompts;
    }

    /// <summary>Returns the child agent definitions. <paramref name="webSearchEnabled"/> adds the hosted web-search tool to market research.</summary>
    public IReadOnlyList<BackgroundAgentDefinition> Definitions(bool webSearchEnabled)
    {
        var marketTools = new List<AITool> { _toolset.Get(ToolNames.ConvertCurrency) };
        if (webSearchEnabled)
        {
            marketTools.Add(new HostedWebSearchTool());
        }

        return
        [
            new BackgroundAgentDefinition(MarketResearch, "Finds the current market price range for equipment and reports it in EUR with sources.", _prompts.MarketResearchInstructions, marketTools),
            new BackgroundAgentDefinition(RiskAnalyst, "Rates each vendor Low/Medium/High risk from its profile and compliance check.", _prompts.RiskAnalystInstructions,
                [_toolset.Get(ToolNames.GetVendorProfile), _toolset.Get(ToolNames.CheckVendorCompliance)]),
        ];
    }

    /// <summary>Creates the bounded child agents over the chat client.</summary>
    public IReadOnlyList<AIAgent> Create(IChatClient chatClient, BackgroundAgentsOptions options, bool webSearchEnabled)
    {
        var gate = new SemaphoreSlim(options.MaxParallel, options.MaxParallel);
        return Definitions(webSearchEnabled).Select(d => Wrap(Build(chatClient, d), gate, options.Timeout)).ToList();
    }

    /// <summary>Wraps any agent with the concurrency gate and timeout used for children.</summary>
    public static AIAgent Wrap(AIAgent agent, SemaphoreSlim gate, TimeSpan timeout) =>
        new TimeoutAgent(new ConcurrencyLimitedAgent(agent, gate), timeout);

    private static ChatClientAgent Build(IChatClient chatClient, BackgroundAgentDefinition definition) =>
        new(chatClient, new ChatClientAgentOptions
        {
            Name = definition.Name,
            Description = definition.Description,
            ChatOptions = new ChatOptions { Instructions = definition.Instructions, Tools = definition.Tools.ToList() },
        });
}
