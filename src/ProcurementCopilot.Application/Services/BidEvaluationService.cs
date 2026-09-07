using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Errors;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Domain.Scoring;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Application.Services;

/// <summary>Scores a bid with the domain service and persists the score into session state.</summary>
public sealed class BidEvaluationService
{
    private readonly IRfpRepository _rfps;
    private readonly IBidRepository _bids;
    private readonly IVendorRepository _vendors;
    private readonly CurrencyService _currency;
    private readonly IEvaluationStateStore _state;

    /// <summary>Initializes the service.</summary>
    public BidEvaluationService(IRfpRepository rfps, IBidRepository bids, IVendorRepository vendors, CurrencyService currency, IEvaluationStateStore state)
    {
        _rfps = rfps;
        _bids = bids;
        _vendors = vendors;
        _currency = currency;
        _state = state;
    }

    /// <summary>Scores one bid relative to its peers and records the result.</summary>
    public async Task<Result<ScoreReport>> ScoreAsync(RfpId rfpId, BidId bidId, CancellationToken cancellationToken = default)
    {
        Rfp? rfp = await _rfps.GetByIdAsync(rfpId, cancellationToken).ConfigureAwait(false);
        if (rfp is null)
        {
            return DomainErrors.NotFound.Rfp;
        }

        Bid? bid = await _bids.GetByIdAsync(bidId, cancellationToken).ConfigureAwait(false);
        if (bid is null)
        {
            return DomainErrors.NotFound.Bid;
        }

        if (bid.RfpId != rfp.Id)
        {
            return DomainErrors.Scoring.BidNotForRfp;
        }

        IReadOnlyList<Bid> all = await _bids.GetByRfpAsync(rfpId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Vendor> vendors = await _vendors.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var scoring = new BidScoringService(await _currency.GetConverterAsync(cancellationToken).ConfigureAwait(false));
        Result<IReadOnlyList<BidScore>> scores = scoring.ScoreAll(rfp, all, vendors.ToDictionary(v => v.Id));
        if (scores.IsFailure)
        {
            return scores.Error;
        }

        int rank = scores.Value.ToList().FindIndex(s => s.BidId == bid.Id) + 1;
        BidScore score = scores.Value[rank - 1];
        EvaluationState state = Persist(rfp, score);
        EvaluationCriteria w = rfp.Criteria;
        return new ScoreReport(rfp.Id.Value, bid.Id.Value, bid.VendorId.Value, score.UnitPriceInRfpCurrency.Amount, rfp.Currency.Value,
            new CriterionBreakdown(score.Criteria.Price, score.Criteria.LeadTime, score.Criteria.Warranty, score.Criteria.Technical, score.Criteria.Sustainability),
            new CriteriaWeights(w.Price, w.LeadTime, w.Warranty, w.Technical, w.Sustainability),
            score.WeightedTotal, rank, state.Scores.Count, all.Count, "BidScoringService (rfp-scoring skill rubric)");
    }

    /// <summary>Summarises what is still outstanding for the active RFP.</summary>
    public async Task<EvaluationProgress> GetProgressAsync(CancellationToken cancellationToken = default)
    {
        EvaluationState state = _state.Get();
        if (state.ActiveRfpId is null || RfpId.Create(state.ActiveRfpId).IsFailure)
        {
            return new EvaluationProgress(null, 0, 0, [], [], false);
        }

        IReadOnlyList<Bid> bids = await _bids.GetByRfpAsync(RfpId.Create(state.ActiveRfpId).Value, cancellationToken).ConfigureAwait(false);
        List<string> unscored = bids.Select(b => b.Id.Value).Where(id => !state.Scores.ContainsKey(id)).ToList();
        List<string> openHits = state.ComplianceHits.Where(h => h.Value.Disposition != ComplianceDisposition.Flagged && h.Value.Disposition != ComplianceDisposition.ClarificationRequested).Select(h => h.Key).ToList();
        bool complete = bids.Count > 0 && unscored.Count == 0 && openHits.Count == 0;
        return new EvaluationProgress(state.ActiveRfpId, bids.Count - unscored.Count, bids.Count, unscored, openHits, complete);
    }

    private EvaluationState Persist(Rfp rfp, BidScore score)
    {
        EvaluationState state = _state.Get();
        state.ActiveRfpId = rfp.Id.Value;
        state.Scores[score.BidId.Value] = new PersistedScore { BidId = score.BidId.Value, VendorId = score.VendorId.Value, WeightedTotal = score.WeightedTotal };
        _state.Save(state);
        return state;
    }
}
