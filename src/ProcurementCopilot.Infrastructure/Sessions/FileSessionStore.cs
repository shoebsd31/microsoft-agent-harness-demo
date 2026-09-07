using System.Text.Json;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Models;

namespace ProcurementCopilot.Infrastructure.Sessions;

/// <summary>
/// Stores sessions as <c>{guid}.json</c> plus <c>{guid}.meta.json</c>. File names are derived from the GUID only,
/// so a session id can never become a path.
/// </summary>
public sealed class FileSessionStore : ISessionStore
{
    private readonly string _directory;
    private readonly IClock _clock;

    /// <summary>Initializes the store for a directory.</summary>
    public FileSessionStore(string directory, IClock clock)
    {
        _directory = Path.GetFullPath(directory);
        _clock = clock;
    }

    /// <summary>Gets the directory sessions are written to.</summary>
    public string Directory => _directory;

    /// <inheritdoc />
    public async Task SaveAsync(Guid sessionId, JsonElement serializedSession, string? title, CancellationToken cancellationToken = default)
    {
        System.IO.Directory.CreateDirectory(_directory);
        string path = PathFor(sessionId, ".json");
        string metaPath = PathFor(sessionId, ".meta.json");
        SessionSummary existing = await ReadMetaAsync(metaPath, cancellationToken).ConfigureAwait(false)
            ?? new SessionSummary(sessionId, _clock.UtcNow, _clock.UtcNow, title ?? "(untitled)");
        var meta = existing with { UpdatedAt = _clock.UtcNow, Title = string.IsNullOrWhiteSpace(existing.Title) || existing.Title == "(untitled)" ? title ?? existing.Title : existing.Title };

        await WriteAtomicAsync(path, serializedSession.GetRawText(), cancellationToken).ConfigureAwait(false);
        await WriteAtomicAsync(metaPath, JsonSerializer.Serialize(meta, ToolJsonContext.Default.SessionSummary), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<JsonElement?> LoadAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        string path = PathFor(sessionId, ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(_directory))
        {
            return [];
        }

        var summaries = new List<SessionSummary>();
        foreach (string meta in System.IO.Directory.EnumerateFiles(_directory, "*.meta.json"))
        {
            SessionSummary? summary = await ReadMetaAsync(meta, cancellationToken).ConfigureAwait(false);
            if (summary is not null)
            {
                summaries.Add(summary);
            }
        }

        return summaries.OrderByDescending(s => s.UpdatedAt).ToList();
    }

    private string PathFor(Guid id, string suffix) => Path.Combine(_directory, id.ToString("N") + suffix);

    private static async Task<SessionSummary?> ReadMetaAsync(string metaPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(metaPath))
        {
            return null;
        }

        try
        {
            string json = await File.ReadAllTextAsync(metaPath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize(json, ToolJsonContext.Default.SessionSummary);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task WriteAtomicAsync(string path, string content, CancellationToken cancellationToken)
    {
        string temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, content, cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
    }
}
