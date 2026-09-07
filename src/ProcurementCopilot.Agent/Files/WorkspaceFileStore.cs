using System.Text;
using Microsoft.Agents.AI;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Agent.Files;

/// <summary>
/// <see cref="AgentFileStore"/> for the harness file-access tools, confined by <see cref="WorkspacePathPolicy"/>:
/// <c>rfps/**</c> read-only, <c>output/**</c> read/write, 1 MB cap, allowed extensions only.
/// </summary>
public sealed class WorkspaceFileStore : AgentFileStore
{
    private readonly FileSystemAgentFileStore _inner;
    private readonly WorkspacePathPolicy _policy;

    /// <summary>Initializes the store over the policy's root.</summary>
    public WorkspaceFileStore(WorkspacePathPolicy policy)
    {
        _policy = policy;
        Directory.CreateDirectory(policy.Root);
        _inner = new FileSystemAgentFileStore(policy.Root);
    }

    /// <inheritdoc />
    public override async Task WriteAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        Check(path, PathAccess.Write);
        Require(_policy.CheckSize(Encoding.UTF8.GetByteCount(content ?? string.Empty)));
        await _inner.WriteAsync(path, content ?? string.Empty, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<string?> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        string full = Check(path, PathAccess.Read);
        Require(_policy.CheckExtension(full));
        if (File.Exists(full))
        {
            Require(_policy.CheckSize(new FileInfo(full).Length));
        }

        return await _inner.ReadAsync(path, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        Check(path, PathAccess.Write);
        return _inner.DeleteAsync(path, cancellationToken);
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<FileStoreEntry>> ListChildrenAsync(string directory, CancellationToken cancellationToken = default)
    {
        CheckDirectory(directory);
        return _inner.ListChildrenAsync(directory, cancellationToken);
    }

    /// <inheritdoc />
    public override Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        Check(path, PathAccess.Read);
        return _inner.FileExistsAsync(path, cancellationToken);
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<FileSearchResult>> SearchAsync(string directory, string regexPattern, string? globPattern = null, bool recursive = false, CancellationToken cancellationToken = default)
    {
        CheckDirectory(directory);
        return _inner.SearchAsync(directory, regexPattern, globPattern, recursive, cancellationToken);
    }

    /// <inheritdoc />
    public override Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        Check(path, PathAccess.Write);
        return _inner.CreateDirectoryAsync(path, cancellationToken);
    }

    private static bool IsRoot(string? path) => string.IsNullOrWhiteSpace(path) || path is "." or "/" or "\\" or "./";

    private void CheckDirectory(string? directory)
    {
        if (!IsRoot(directory))
        {
            Check(directory!, PathAccess.Read);
        }
    }

    private string Check(string path, PathAccess access) => Require(_policy.Resolve(path, access));

    private static T Require<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw new UnauthorizedAccessException($"{result.Error.Code}: {result.Error.Message}");
}
