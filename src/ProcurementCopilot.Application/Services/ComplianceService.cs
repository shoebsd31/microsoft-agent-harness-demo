using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Compliance;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Errors;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Application.Services;

/// <summary>Checks vendors against the sanctions list and required certifications, recording a disposition for every hit.</summary>
public sealed class ComplianceService
{
    private readonly IVendorRepository _vendors;
    private readonly IRfpRepository _rfps;
    private readonly ISanctionsRepository _sanctions;
    private readonly IEvaluationStateStore _state;

    /// <summary>Initializes the service.</summary>
    public ComplianceService(IVendorRepository vendors, IRfpRepository rfps, ISanctionsRepository sanctions, IEvaluationStateStore state)
    {
        _vendors = vendors;
        _rfps = rfps;
        _sanctions = sanctions;
        _state = state;
    }

    /// <summary>Checks the vendor in the context of the active RFP (or the first open RFP when none is active).</summary>
    public async Task<Result<ComplianceReport>> CheckAsync(VendorId vendorId, CancellationToken cancellationToken = default)
    {
        Vendor? vendor = await _vendors.GetByIdAsync(vendorId, cancellationToken).ConfigureAwait(false);
        if (vendor is null)
        {
            return DomainErrors.NotFound.Vendor;
        }

        Rfp? rfp = await ResolveRfpAsync(cancellationToken).ConfigureAwait(false);
        if (rfp is null)
        {
            return DomainErrors.NotFound.Rfp;
        }

        IReadOnlyList<SanctionsEntry> sanctions = await _sanctions.GetAllAsync(cancellationToken).ConfigureAwait(false);
        ComplianceResult result = ComplianceChecker.Check(vendor, rfp, sanctions);
        IReadOnlyList<string> required = rfp.RequiredCertifications.Count > 0
            ? rfp.RequiredCertifications
            : ComplianceChecker.RequiredByCategory.GetValueOrDefault(rfp.Category, []);

        string disposition = result.HasIssues ? RecordHit(vendor.Id, result).ToString() : "None";
        return new ComplianceReport(vendor.Id.Value, vendor.Name, result.IsSanctioned, result.Sanction?.ListName, result.Sanction?.Reason,
            required, result.MissingCertifications, result.Verdict, disposition, "data/sanctions.csv + data/vendors.json (seeded)");
    }

    private async Task<Rfp?> ResolveRfpAsync(CancellationToken cancellationToken)
    {
        EvaluationState state = _state.Get();
        if (state.ActiveRfpId is not null && RfpId.Create(state.ActiveRfpId) is { IsSuccess: true } id)
        {
            Rfp? active = await _rfps.GetByIdAsync(id.Value, cancellationToken).ConfigureAwait(false);
            if (active is not null)
            {
                return active;
            }
        }

        IReadOnlyList<Rfp> all = await _rfps.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(r => r.IsOpen);
    }

    private ComplianceDisposition RecordHit(VendorId vendorId, ComplianceResult result)
    {
        EvaluationState state = _state.Get();
        if (state.ComplianceHits.TryGetValue(vendorId.Value, out PersistedComplianceHit? existing))
        {
            existing.Verdict = result.Verdict;
            _state.Save(state);
            return existing.Disposition;
        }

        state.ComplianceHits[vendorId.Value] = new PersistedComplianceHit { VendorId = vendorId.Value, Verdict = result.Verdict, Disposition = ComplianceDisposition.Flagged };
        _state.Save(state);
        return ComplianceDisposition.Flagged;
    }
}
