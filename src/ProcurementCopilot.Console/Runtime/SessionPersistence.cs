using System.Text.Json;
using Microsoft.Agents.AI;
using ProcurementCopilot.Agent.Sessions;
using ProcurementCopilot.Application.Abstractions;

namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>Explicit session save used at turn end and on exit (the history provider also checkpoints after every model call).</summary>
public static class SessionPersistence
{
    /// <summary>Serialises and stores the session; returns its id.</summary>
    public static async Task<Guid> SaveAsync(AIAgent agent, AgentSession session, ISessionStore store, string? title, CancellationToken cancellationToken = default)
    {
        Guid id = SessionIdentity.GetOrCreate(session);
        JsonElement json = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken).ConfigureAwait(false);
        await store.SaveAsync(id, json, title, cancellationToken).ConfigureAwait(false);
        return id;
    }
}
