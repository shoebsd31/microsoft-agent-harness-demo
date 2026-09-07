using Microsoft.Agents.AI;
using ProcurementCopilot.Agent.State;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Agent.Looping;

/// <summary>
/// Predicate loop evaluator: keeps re-invoking the agent (in execute mode) until every bid of the active RFP has a
/// persisted score and every compliance hit has a disposition. Feedback lists exactly what is outstanding.
/// </summary>
public sealed class AllBidsScoredEvaluator : LoopEvaluator
{
    private readonly IBidRepository _bids;
    private readonly int _maxIterations;

    /// <summary>Initializes the evaluator.</summary>
    public AllBidsScoredEvaluator(IBidRepository bids, int maxIterations)
    {
        _bids = bids;
        _maxIterations = maxIterations;
    }

    /// <summary>Gets the outstanding items from the last evaluation, for the console to report on exhaustion.</summary>
    public IReadOnlyList<string> LastOutstanding { get; private set; } = [];

    /// <inheritdoc />
    public override async ValueTask<LoopEvaluation> EvaluateAsync(LoopContext context, CancellationToken cancellationToken = default)
    {
        AgentModeProvider? modes = context.Agent.GetService<AgentModeProvider>();
        if (modes is not null && !string.Equals(await modes.GetModeAsync(context.Session, cancellationToken).ConfigureAwait(false), AgentModes.Execute, StringComparison.OrdinalIgnoreCase))
        {
            LastOutstanding = [];
            return LoopEvaluation.Stop();
        }

        IReadOnlyList<string> outstanding = await OutstandingAsync(context.Session, cancellationToken).ConfigureAwait(false);
        LastOutstanding = outstanding;
        if (outstanding.Count == 0)
        {
            return LoopEvaluation.Stop();
        }

        bool lastChance = context.Iteration >= _maxIterations - 1;
        string feedback = "The evaluation is not complete. Outstanding: " + string.Join("; ", outstanding) + "." +
            (lastChance ? " This is the final iteration: report exactly what remains outstanding and why." : " Continue with the next outstanding item.");
        return LoopEvaluation.Continue(feedback);
    }

    /// <summary>Lists the outstanding items for a session: unscored bids and compliance hits without a disposition.</summary>
    public async Task<IReadOnlyList<string>> OutstandingAsync(AgentSession session, CancellationToken cancellationToken = default)
    {
        EvaluationState state = SessionEvaluationStateStore.Read(session);
        if (state.ActiveRfpId is null || !RfpId.IsValid(state.ActiveRfpId))
        {
            return [];
        }

        IReadOnlyList<Bid> bids = await _bids.GetByRfpAsync(RfpId.Create(state.ActiveRfpId).Value, cancellationToken).ConfigureAwait(false);
        var outstanding = new List<string>();
        outstanding.AddRange(bids.Where(b => !state.Scores.ContainsKey(b.Id.Value)).Select(b => $"score {b.Id.Value} (score_bid)"));
        outstanding.AddRange(state.ComplianceHits.Values
            .Where(h => h.Disposition is not ComplianceDisposition.Flagged and not ComplianceDisposition.ClarificationRequested)
            .Select(h => $"disposition for compliance hit on {h.VendorId}"));
        return outstanding;
    }
}
