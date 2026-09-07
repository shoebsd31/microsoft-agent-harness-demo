using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Errors;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Application.Services;

/// <summary>Drafts clarification emails into the outbox. Never sends anything.</summary>
public sealed class ClarificationService
{
    /// <summary>Tool name used in the audit trail.</summary>
    public const string ToolName = "draft_clarification_email";

    private readonly IVendorRepository _vendors;
    private readonly IOutbox _outbox;
    private readonly IAuditLog _audit;
    private readonly IEvaluationStateStore _state;
    private readonly IClock _clock;

    /// <summary>Initializes the service.</summary>
    public ClarificationService(IVendorRepository vendors, IOutbox outbox, IAuditLog audit, IEvaluationStateStore state, IClock clock)
    {
        _vendors = vendors;
        _outbox = outbox;
        _audit = audit;
        _state = state;
        _clock = clock;
    }

    /// <summary>Validates, writes the draft to the outbox, audits the action and records the disposition.</summary>
    public async Task<Result<ClarificationDraftReport>> DraftAsync(VendorId vendorId, string subject, string body, CancellationToken cancellationToken = default)
    {
        Vendor? vendor = await _vendors.GetByIdAsync(vendorId, cancellationToken).ConfigureAwait(false);
        if (vendor is null)
        {
            return DomainErrors.NotFound.Vendor;
        }

        var draft = new EmailDraft(Guid.NewGuid(), vendor.Id.Value, subject, body, _clock.UtcNow);
        string savedTo = await _outbox.SaveDraftAsync(draft, cancellationToken).ConfigureAwait(false);
        string hash = ArgumentHasher.Hash(vendor.Id.Value, subject, body);
        await _audit.RecordActionAsync(new ActionRecord(_clock.UtcNow, ToolName, hash, "ok", savedTo), cancellationToken).ConfigureAwait(false);

        string disposition = RecordDisposition(vendor.Id);
        return new ClarificationDraftReport(draft.Id.ToString("N"), vendor.Id.Value, subject, savedTo, disposition);
    }

    private string RecordDisposition(VendorId vendorId)
    {
        EvaluationState state = _state.Get();
        if (state.ComplianceHits.TryGetValue(vendorId.Value, out PersistedComplianceHit? hit))
        {
            hit.Disposition = ComplianceDisposition.ClarificationRequested;
        }

        if (!state.OpenClarifications.Contains(vendorId.Value, StringComparer.Ordinal))
        {
            state.OpenClarifications.Add(vendorId.Value);
        }

        _state.Save(state);
        return hit is null ? "ClarificationRequested (no compliance hit on file)" : hit.Disposition.ToString();
    }
}
