using Microsoft.Agents.AI;

namespace ProcurementCopilot.Agent.Sessions;

/// <summary>Assigns every session a GUID stored in its state bag; the GUID is the only thing that ever becomes a file name.</summary>
public static class SessionIdentity
{
    /// <summary>State-bag key.</summary>
    public const string StateKey = "ProcurementCopilot.SessionId";

    /// <summary>Returns the session's id, creating one when missing.</summary>
    public static Guid GetOrCreate(AgentSession session)
    {
        Guid? existing = TryGet(session);
        if (existing is not null)
        {
            return existing.Value;
        }

        var id = Guid.NewGuid();
        session.StateBag.SetValue(StateKey, id.ToString("N"));
        return id;
    }

    /// <summary>Returns the session's id when one has been assigned.</summary>
    public static Guid? TryGet(AgentSession session) =>
        session.StateBag.TryGetValue(StateKey, out string? text) && Guid.TryParse(text, out Guid id) && id != Guid.Empty ? id : null;
}
