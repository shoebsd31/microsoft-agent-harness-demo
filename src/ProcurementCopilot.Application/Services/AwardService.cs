using System.Globalization;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Currency;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Errors;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Application.Services;

/// <summary>Records an award recommendation (never an award) through <see cref="IAwardRecorder"/> and the audit trail.</summary>
public sealed class AwardService
{
    /// <summary>Tool name used in the audit trail.</summary>
    public const string ToolName = "record_award_recommendation";

    private readonly IRfpRepository _rfps;
    private readonly IVendorRepository _vendors;
    private readonly IBidRepository _bids;
    private readonly ISanctionsRepository _sanctions;
    private readonly CurrencyService _currency;
    private readonly IAwardRecorder _recorder;
    private readonly IAuditLog _audit;
    private readonly IEvaluationStateStore _state;
    private readonly IClock _clock;

    /// <summary>Initializes the service.</summary>
    public AwardService(IRfpRepository rfps, IVendorRepository vendors, IBidRepository bids, ISanctionsRepository sanctions, CurrencyService currency,
        IAwardRecorder recorder, IAuditLog audit, IEvaluationStateStore state, IClock clock)
    {
        _rfps = rfps;
        _vendors = vendors;
        _bids = bids;
        _sanctions = sanctions;
        _currency = currency;
        _recorder = recorder;
        _audit = audit;
        _state = state;
        _clock = clock;
    }

    /// <summary>Validates the recommendation, refuses sanctioned vendors, records it and audits the outcome.</summary>
    public async Task<Result<AwardRecommendationReport>> RecordAsync(RfpId rfpId, VendorId vendorId, string rationale, CancellationToken cancellationToken = default)
    {
        Rfp? rfp = await _rfps.GetByIdAsync(rfpId, cancellationToken).ConfigureAwait(false);
        if (rfp is null)
        {
            return DomainErrors.NotFound.Rfp;
        }

        Vendor? vendor = await _vendors.GetByIdAsync(vendorId, cancellationToken).ConfigureAwait(false);
        if (vendor is null)
        {
            return DomainErrors.NotFound.Vendor;
        }

        string hash = ArgumentHasher.Hash(rfpId.Value, vendorId.Value, rationale);
        IReadOnlyList<SanctionsEntry> sanctions = await _sanctions.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (sanctions.Any(s => s.VendorId == vendor.Id))
        {
            await _audit.RecordActionAsync(new ActionRecord(_clock.UtcNow, ToolName, hash, "blocked", DomainErrors.Award.VendorSanctioned.Code), cancellationToken).ConfigureAwait(false);
            return DomainErrors.Award.VendorSanctioned;
        }

        Result<(decimal Price, int Lead)> terms = await WinningTermsAsync(rfp, vendor.Id, cancellationToken).ConfigureAwait(false);
        if (terms.IsFailure)
        {
            return terms.Error;
        }

        EvaluationState state = _state.Get();
        decimal? winning = state.Scores.Values.FirstOrDefault(s => string.Equals(s.VendorId, vendorId.Value, StringComparison.Ordinal))?.WeightedTotal;
        DateTimeOffset now = _clock.UtcNow;
        Result<AwardRecordReference> recorded = await _recorder.RecordAsync(
            new AwardRecordRequest(rfpId, vendorId, vendor.Name, winning, rationale, terms.Value.Price, terms.Value.Lead, now), cancellationToken).ConfigureAwait(false);
        if (recorded.IsFailure)
        {
            await _audit.RecordActionAsync(new ActionRecord(now, ToolName, hash, "failed", recorded.Error.Code), cancellationToken).ConfigureAwait(false);
            return recorded.Error;
        }

        await _audit.RecordActionAsync(new ActionRecord(now, ToolName, hash, "ok", recorded.Value.Reference), cancellationToken).ConfigureAwait(false);
        state.AwardedVendorId = vendorId.Value;
        _state.Save(state);
        return new AwardRecommendationReport(rfpId.Value, vendorId.Value, winning, recorded.Value.Reference, now.ToString("O", CultureInfo.InvariantCulture), recorded.Value.PurchaseOrderId);
    }

    /// <summary>Finds the vendor's bid for the RFP and converts its unit price into the RFP currency.</summary>
    private async Task<Result<(decimal Price, int Lead)>> WinningTermsAsync(Rfp rfp, VendorId vendorId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Bid> bids = await _bids.GetByRfpAsync(rfp.Id, cancellationToken).ConfigureAwait(false);
        Bid? bid = bids.FirstOrDefault(b => b.VendorId == vendorId);
        if (bid is null)
        {
            return Error.Validation("Award.NoBid", $"Vendor {vendorId.Value} has not submitted a bid for {rfp.Id.Value}.");
        }

        CurrencyConverter converter = await _currency.GetConverterAsync(cancellationToken).ConfigureAwait(false);
        return converter.Convert(bid.UnitPrice, rfp.Currency).Map(price => (price.Amount, bid.LeadTimeWeeks));
    }
}
