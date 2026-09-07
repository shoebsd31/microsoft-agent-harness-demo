using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcurementCopilot.Agent;
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.ConsoleApp.Hosting;
using ProcurementCopilot.Testing;
using ProcurementCopilot.Testing.Demo;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>Builds the agent (live Foundry client or the scripted fake) and the initial session.</summary>
public sealed class AgentBootstrapper
{
    private readonly HarnessAgentFactory _factory;
    private readonly ProcurementAgentOptions _options;
    private readonly IChatClientFactory _clients;
    private readonly ISessionStore _sessions;
    private readonly IOptions<FoundryOptions> _foundry;
    private readonly ILogger<AgentBootstrapper> _logger;

    /// <summary>Initializes the bootstrapper.</summary>
    public AgentBootstrapper(HarnessAgentFactory factory, ProcurementAgentOptions options, IChatClientFactory clients, ISessionStore sessions, IOptions<FoundryOptions> foundry, ILogger<AgentBootstrapper> logger)
    {
        _factory = factory;
        _options = options;
        _clients = clients;
        _sessions = sessions;
        _foundry = foundry;
        _logger = logger;
    }

    /// <summary>Creates the agent for the launch mode and populates the state.</summary>
    public async Task InitializeAsync(AppState state, LaunchOptions launch)
    {
        state.Factory = _factory;
        state.FakeMode = launch.Fake || !_foundry.Value.IsConfigured;
        if (state.FakeMode && !launch.Fake)
        {
            _logger.LogWarning("Foundry is not configured ({Missing}); falling back to the scripted fake client.", string.Join(", ", _foundry.Value.MissingKeys()));
        }

        state.Agent = state.FakeMode ? CreateFakeAgent() : CreateLiveAgent();
        state.Session = launch.ResumeSessionId is { } id && await TryResumeAsync(state.Agent, id).ConfigureAwait(false) is { } resumed
            ? resumed
            : await state.Agent.CreateSessionAsync().ConfigureAwait(false);
        SessionIdentity.GetOrCreate(state.Session);
    }

    /// <summary>Loads and deserializes a stored session, or returns <see langword="null"/>.</summary>
    public async Task<AgentSession?> TryResumeAsync(AIAgent agent, Guid id)
    {
        JsonElement? stored = await _sessions.LoadAsync(id).ConfigureAwait(false);
        return stored is null ? null : await agent.DeserializeSessionAsync(stored.Value).ConfigureAwait(false);
    }

    private HarnessAgent CreateLiveAgent()
    {
        IChatClient chat = _clients.CreateChatClient();
        IChatClient? judge = _options.Agent.EnableJudge ? _clients.CreateJudgeClient() : null;
        return _factory.Create(chat, _options, judge);
    }

    private HarnessAgent CreateFakeAgent()
    {
        var main = new ScriptedChatClient(DemoScript.Main(), supportsWebSearch: false) { StreamDelay = TimeSpan.FromMilliseconds(12) };
        var background = new InstructionRoutedChatClient(new Dictionary<string, IChatClient>(StringComparer.OrdinalIgnoreCase)
        {
            ["market-research"] = new ScriptedChatClient(DemoScript.MarketResearch()),
            ["risk-analyst"] = new ScriptedChatClient(DemoScript.RiskAnalyst()),
        }, new ScriptedChatClient());
        return _factory.Create(main, _options, judgeClient: null, backgroundChatClient: background);
    }
}
