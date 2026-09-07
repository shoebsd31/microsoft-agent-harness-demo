using System.Text.Json;

namespace ProcurementCopilot.Application.Abstractions;

/// <summary>Summary of a persisted session.</summary>
/// <param name="Id">Session id (GUID).</param>
/// <param name="CreatedAt">When the session was first saved.</param>
/// <param name="UpdatedAt">When the session was last saved.</param>
/// <param name="Title">Short label, typically the first user prompt.</param>
public sealed record SessionSummary(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Title);

/// <summary>Persists serialized agent sessions. File names are always the GUID only.</summary>
public interface ISessionStore
{
    /// <summary>Saves (overwrites) the serialized session.</summary>
    Task SaveAsync(Guid sessionId, JsonElement serializedSession, string? title, CancellationToken cancellationToken = default);

    /// <summary>Loads a serialized session, or <see langword="null"/> when it does not exist.</summary>
    Task<JsonElement?> LoadAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Lists stored sessions, newest first.</summary>
    Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken cancellationToken = default);
}
