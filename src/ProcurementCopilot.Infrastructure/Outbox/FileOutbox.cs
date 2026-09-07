using System.Globalization;
using System.Text;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Infrastructure.Outbox;

/// <summary>Writes drafts and documents under <c>workspace/output</c>, confined by <see cref="WorkspacePathPolicy"/>.</summary>
public sealed class FileOutbox : IOutbox
{
    private readonly WorkspacePathPolicy _paths;

    /// <summary>Initializes the outbox.</summary>
    public FileOutbox(WorkspacePathPolicy paths) => _paths = paths;

    /// <inheritdoc />
    public Task<string> SaveDraftAsync(EmailDraft draft, CancellationToken cancellationToken = default)
    {
        string name = $"{draft.CreatedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{draft.VendorId}-{draft.Id:N}.md";
        var content = new StringBuilder()
            .AppendLine("---")
            .Append("to: ").AppendLine(draft.VendorId)
            .Append("subject: ").AppendLine(draft.Subject)
            .Append("created: ").AppendLine(draft.CreatedAt.ToString("O", CultureInfo.InvariantCulture))
            .AppendLine("status: DRAFT (not sent)")
            .AppendLine("---")
            .AppendLine()
            .AppendLine(draft.Body)
            .ToString();
        return SaveDocumentAsync($"outbox/{name}", content, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> SaveDocumentAsync(string relativePath, string content, CancellationToken cancellationToken = default)
    {
        string relative = "output/" + relativePath.Replace('\\', '/').TrimStart('/');
        Result<string> resolved = _paths.Resolve(relative, PathAccess.Write);
        if (resolved.IsFailure)
        {
            throw new InvalidOperationException($"Outbox path rejected by workspace policy: {resolved.Error}");
        }

        Result<long> size = _paths.CheckSize(Encoding.UTF8.GetByteCount(content));
        if (size.IsFailure)
        {
            throw new InvalidOperationException(size.Error.Message);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(resolved.Value)!);
        await File.WriteAllTextAsync(resolved.Value, content, cancellationToken).ConfigureAwait(false);
        return relative;
    }
}
