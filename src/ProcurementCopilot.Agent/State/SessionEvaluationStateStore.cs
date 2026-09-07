using System.Text.Json;
using Microsoft.Agents.AI;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Models;

namespace ProcurementCopilot.Agent.State;

/// <summary>
/// Stores <see cref="EvaluationState"/> in the <see cref="AgentSession.StateBag"/> of the running session
/// (resolved from <see cref="AIAgent.CurrentRunContext"/>), so scores survive compaction and session resume.
/// Outside a run, an ambient session can be supplied with <see cref="UseSession"/>.
/// </summary>
public sealed class SessionEvaluationStateStore : IEvaluationStateStore
{
    /// <summary>State-bag key.</summary>
    public const string StateKey = "ProcurementCopilot.EvaluationState";

    private static readonly JsonSerializerOptions JsonOptions = new() { TypeInfoResolver = ToolJsonContext.Default };
    private static readonly ProviderSessionState<EvaluationState> State = new(_ => new EvaluationState(), StateKey, JsonOptions);
    private static readonly AsyncLocal<AgentSession?> Ambient = new();
    private readonly EvaluationState _detached = new();

    /// <summary>Reads the state stored in a specific session.</summary>
    public static EvaluationState Read(AgentSession session) => State.GetOrInitializeState(session);

    /// <summary>Writes the state into a specific session.</summary>
    public static void Write(AgentSession session, EvaluationState state) => State.SaveState(session, state);

    /// <summary>Makes <paramref name="session"/> the ambient session for code running outside an agent run (console commands).</summary>
    public static IDisposable UseSession(AgentSession session)
    {
        AgentSession? previous = Ambient.Value;
        Ambient.Value = session;
        return new Scope(() => Ambient.Value = previous);
    }

    /// <inheritdoc />
    public EvaluationState Get()
    {
        AgentSession? session = Current();
        return session is null ? _detached : Read(session);
    }

    /// <inheritdoc />
    public void Save(EvaluationState state)
    {
        AgentSession? session = Current();
        if (session is not null)
        {
            Write(session, state);
        }
    }

    private static AgentSession? Current() => AIAgent.CurrentRunContext?.Session ?? Ambient.Value;

    private sealed class Scope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
