namespace ProcurementCopilot.Application.Abstractions;

/// <summary>A drafted outbound email. Nothing is ever sent; drafts land in the workspace outbox.</summary>
/// <param name="Id">Draft id.</param>
/// <param name="VendorId">Recipient vendor.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="Body">Body text.</param>
/// <param name="CreatedAt">Creation time.</param>
public sealed record EmailDraft(Guid Id, string VendorId, string Subject, string Body, DateTimeOffset CreatedAt);

/// <summary>Stores email drafts and other side-effect artefacts under <c>workspace/output</c>.</summary>
public interface IOutbox
{
    /// <summary>Writes the draft and returns the relative path it was written to.</summary>
    Task<string> SaveDraftAsync(EmailDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Writes an arbitrary JSON document under the output folder and returns its relative path.</summary>
    Task<string> SaveDocumentAsync(string relativePath, string content, CancellationToken cancellationToken = default);
}
