using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Errors;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Application.Services;

/// <summary>Read-only queries used by the read tools. Vendor-authored text is always wrapped as untrusted data.</summary>
public sealed class RfpQueryService
{
    private readonly IRfpRepository _rfps;
    private readonly IBidRepository _bids;
    private readonly IVendorRepository _vendors;
    private readonly IEvaluationStateStore _state;

    /// <summary>Initializes the service.</summary>
    public RfpQueryService(IRfpRepository rfps, IBidRepository bids, IVendorRepository vendors, IEvaluationStateStore state)
    {
        _rfps = rfps;
        _bids = bids;
        _vendors = vendors;
        _state = state;
    }

    /// <summary>Lists RFPs that are still open.</summary>
    public async Task<IReadOnlyList<RfpSummary>> ListOpenAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Rfp> all = await _rfps.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(r => r.IsOpen)
            .Select(r => new RfpSummary(r.Id.Value, r.Title, r.Status.ToString(), r.Currency.Value, r.Quantity, r.Category))
            .ToList();
    }

    /// <summary>Returns RFP detail and marks it as the active RFP of the session.</summary>
    public async Task<Result<RfpDetail>> GetAsync(RfpId id, CancellationToken cancellationToken = default)
    {
        Rfp? rfp = await _rfps.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (rfp is null)
        {
            return DomainErrors.NotFound.Rfp;
        }

        IReadOnlyList<Bid> bids = await _bids.GetByRfpAsync(id, cancellationToken).ConfigureAwait(false);
        MarkActive(rfp.Id);
        EvaluationCriteria c = rfp.Criteria;
        return new RfpDetail(rfp.Id.Value, rfp.Title, rfp.Description, rfp.Status.ToString(), rfp.Currency.Value, rfp.Quantity, rfp.Category,
            new CriteriaWeights(c.Price, c.LeadTime, c.Warranty, c.Technical, c.Sustainability), rfp.RequiredCertifications, bids.Count);
    }

    /// <summary>Lists the bids of an RFP with vendor-authored text wrapped.</summary>
    public async Task<Result<IReadOnlyList<BidSummary>>> ListBidsAsync(RfpId id, CancellationToken cancellationToken = default)
    {
        Rfp? rfp = await _rfps.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (rfp is null)
        {
            return DomainErrors.NotFound.Rfp;
        }

        MarkActive(rfp.Id);
        IReadOnlyList<Bid> bids = await _bids.GetByRfpAsync(id, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Vendor> vendors = await _vendors.GetAllAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<VendorId, Vendor> byId = vendors.ToDictionary(v => v.Id);
        return bids.Select(b => new BidSummary(
                b.Id.Value, b.RfpId.Value, b.VendorId.Value,
                byId.TryGetValue(b.VendorId, out Vendor? v) ? v.Name : "(unknown vendor)",
                b.UnitPrice.Amount, b.UnitPrice.Currency.Value, b.LeadTimeWeeks, b.WarrantyMonths, b.TechnicalCompliancePercent,
                UntrustedDataEnvelope.ForBid(b.Id.Value, b.DeliveryClause),
                UntrustedDataEnvelope.ForBid(b.Id.Value, b.Notes)))
            .ToList();
    }

    /// <summary>Returns a vendor profile with the notes wrapped as untrusted data.</summary>
    public async Task<Result<VendorProfile>> GetVendorAsync(VendorId id, CancellationToken cancellationToken = default)
    {
        Vendor? vendor = await _vendors.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return vendor is null
            ? DomainErrors.NotFound.Vendor
            : new VendorProfile(vendor.Id.Value, vendor.Name, vendor.Country, vendor.Certifications, vendor.YearsTrading,
                UntrustedDataEnvelope.ForVendor(vendor.Id.Value, vendor.Notes));
    }

    private void MarkActive(RfpId id)
    {
        EvaluationState state = _state.Get();
        if (!string.Equals(state.ActiveRfpId, id.Value, StringComparison.Ordinal))
        {
            state.ActiveRfpId = id.Value;
            _state.Save(state);
        }
    }
}
