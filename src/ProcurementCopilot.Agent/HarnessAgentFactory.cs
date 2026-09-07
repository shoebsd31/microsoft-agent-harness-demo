using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProcurementCopilot.Agent.Approval;
using ProcurementCopilot.Agent.Background;
using ProcurementCopilot.Agent.Compaction;
using ProcurementCopilot.Agent.Files;
using ProcurementCopilot.Agent.Looping;
using ProcurementCopilot.Agent.Prompts;
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Agent.Shell;
using ProcurementCopilot.Agent.Skills;
using ProcurementCopilot.Agent.Telemetry;
using ProcurementCopilot.Agent.Tools;
using ProcurementCopilot.Agent.WebSearch;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Repositories;

namespace ProcurementCopilot.Agent;

/// <summary>
/// The single place where the Procurement Copilot harness is composed. Every <see cref="HarnessAgentOptions"/> switch is
/// set explicitly so the capability map (docs/ARCHITECTURE.md) can be read from this one file.
/// </summary>
public sealed class HarnessAgentFactory
{
    private readonly ProcurementToolset _toolset;
    private readonly ConfinedShellTool _shell;
    private readonly BackgroundAgentFactory _backgroundAgents;
    private readonly ApprovalPolicy _approvals;
    private readonly WorkspacePathPolicy _paths;
    private readonly ISessionStore _sessions;
    private readonly IBidRepository _bids;
    private readonly PromptCatalog _prompts;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HarnessAgentFactory> _logger;

    /// <summary>Initializes the factory.</summary>
    public HarnessAgentFactory(ProcurementToolset toolset, ConfinedShellTool shell, BackgroundAgentFactory backgroundAgents, ApprovalPolicy approvals,
        WorkspacePathPolicy paths, ISessionStore sessions, IBidRepository bids, PromptCatalog prompts, ILoggerFactory? loggerFactory = null)
    {
        _toolset = toolset;
        _shell = shell;
        _backgroundAgents = backgroundAgents;
        _approvals = approvals;
        _paths = paths;
        _sessions = sessions;
        _bids = bids;
        _prompts = prompts;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<HarnessAgentFactory>();
    }

    /// <summary>Gets the predicate loop evaluator created by the last <see cref="BuildOptions"/> call.</summary>
    public AllBidsScoredEvaluator? LoopEvaluator { get; private set; }

    /// <summary>Gets the compaction strategy created by the last <see cref="BuildOptions"/> call.</summary>
    public ProcurementCompactionStrategy? CompactionStrategy { get; private set; }

    /// <summary>Gets the checkpointing history provider created by the last <see cref="BuildOptions"/> call.</summary>
    public CheckpointingChatHistoryProvider? HistoryProvider { get; private set; }

    /// <summary>Creates the harness agent. <paramref name="backgroundChatClient"/> lets child agents use a different client (fake mode).</summary>
    public HarnessAgent Create(IChatClient chatClient, ProcurementAgentOptions options, IChatClient? judgeClient = null, IChatClient? backgroundChatClient = null) =>
        chatClient.AsHarnessAgent(BuildOptions(chatClient, options, judgeClient, backgroundChatClient), _loggerFactory);

    /// <summary>Builds the fully explicit <see cref="HarnessAgentOptions"/>. Public so tests can assert every switch.</summary>
    public HarnessAgentOptions BuildOptions(IChatClient chatClient, ProcurementAgentOptions options, IChatClient? judgeClient = null, IChatClient? backgroundChatClient = null)
    {
        bool webSearch = options.Agent.EnableWebSearch && WebSearchSupport.IsSupported(chatClient);
        if (options.Agent.EnableWebSearch && !webSearch)
        {
            _logger.LogWarning("Hosted web search is enabled in configuration but the chat client does not support it; continuing without web search.");
        }

        LoopEvaluator = new AllBidsScoredEvaluator(_bids, options.Agent.MaxLoopIterations);
        var evaluators = new List<LoopEvaluator> { LoopEvaluator };
        if (options.Agent.EnableJudge && judgeClient is not null)
        {
            evaluators.Add(JudgeEvaluatorFactory.Create(judgeClient));
        }

        CompactionStrategy = new ProcurementCompactionStrategy(options.Agent.MaxContextWindowTokens, options.Agent.MaxOutputTokens);
        var history = new InMemoryChatHistoryProvider();
        HistoryProvider = options.PersistSessions ? new CheckpointingChatHistoryProvider(history, _sessions, _loggerFactory.CreateLogger<CheckpointingChatHistoryProvider>()) : null;

        return new HarnessAgentOptions
        {
            Name = "procurement-copilot",
            Description = "Contoso Procurement Copilot: evaluates RFP bids, checks compliance and recommends a vendor.",
            HarnessInstructions = HarnessAgent.DefaultInstructions + "\n\n" + _prompts.HarnessAddendum,
            ChatOptions = new ChatOptions
            {
                Instructions = _prompts.AgentInstructions,
                Tools = [.. _toolset.AllTools, _toolset.Wrap(_shell.Create())],
                MaxOutputTokens = options.Agent.MaxOutputTokens,
            },
            MaxContextWindowTokens = options.Agent.MaxContextWindowTokens,   // §4.3 compaction
            MaxOutputTokens = options.Agent.MaxOutputTokens,
            CompactionStrategy = CompactionStrategy,                        // §4.3 custom strategy
            DisableCompaction = false,
            ChatHistoryProvider = (ChatHistoryProvider?)HistoryProvider ?? history, // §4.2 per-service-call persistence
            MaximumIterationsPerRequest = options.Agent.MaxFunctionInvocationIterations, // §4.1 iteration limit
            DisableToolAutoApproval = false,                                 // §4.7 standing approvals + rules
            ToolApprovalAgentOptions = new ToolApprovalAgentOptions { AutoApprovalRules = ApprovalRuleBuilder.Build(_approvals) },
            DisableApprovalNotRequiredFunctionBypassing = false,
            DisableApprovalResponseBinding = false,
            DisableFileMemory = false,                                       // §4.6 session file memory
            FileMemoryStore = new FileSystemAgentFileStore(options.FileMemoryRoot ?? Path.Combine(_paths.Root, "agent-file-memory")),
            FileAccessStore = new WorkspaceFileStore(_paths),                // §4.6 confined file access (opt-in)
            FileAccessProviderOptions = new FileAccessProviderOptions { DisableWriteTools = false, DisableReadOnlyToolApproval = false, DisableWriteToolApproval = false },
            DisableWebSearch = !webSearch,                                   // §4.9 hosted web search
            DisableTodoProvider = false,                                     // §4.4 todos
            DisableAgentModeProvider = false,                                // §4.5 plan/execute
            AgentModeProviderOptions = new AgentModeProviderOptions { DefaultMode = AgentModes.Plan },
            DisableAgentSkillsProvider = false,                              // §4.10 skills from the base directory
            AgentSkillsSource = SkillsSourceFactory.Create(options.SkillsPath, _loggerFactory),
            DisableOpenTelemetry = false,                                    // §4.8 telemetry
            OpenTelemetrySourceName = AgentTelemetry.SourceName,
            BackgroundAgents = _backgroundAgents.Create(backgroundChatClient ?? chatClient, options.Agent.BackgroundAgents, webSearch), // §4.11
            BackgroundAgentsProviderOptions = new BackgroundAgentsProviderOptions { WaitTimeout = options.Agent.BackgroundAgents.Timeout },
            LoopEvaluators = evaluators,                                     // §4.13 looping
            LoopAgentOptions = new LoopAgentOptions { MaxIterations = options.Agent.MaxLoopIterations },
            AIContextProviders = [],
        };
    }
}
