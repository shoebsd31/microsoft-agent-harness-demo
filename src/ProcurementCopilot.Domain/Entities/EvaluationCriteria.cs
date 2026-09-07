using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Errors;

namespace ProcurementCopilot.Domain.Entities;

/// <summary>
/// Weighted evaluation criteria for an RFP. Weights are relative and are normalised by the scoring service.
/// </summary>
/// <param name="Price">Weight of the unit price criterion.</param>
/// <param name="LeadTime">Weight of the delivery lead-time criterion.</param>
/// <param name="Warranty">Weight of the warranty criterion.</param>
/// <param name="Technical">Weight of the technical compliance criterion.</param>
/// <param name="Sustainability">Weight of the sustainability certification criterion.</param>
public sealed record EvaluationCriteria(decimal Price, decimal LeadTime, decimal Warranty, decimal Technical, decimal Sustainability)
{
    /// <summary>Gets the sum of all weights.</summary>
    public decimal Total => Price + LeadTime + Warranty + Technical + Sustainability;

    /// <summary>Validates the weights: none negative and the total positive.</summary>
    public Result<EvaluationCriteria> Validate() =>
        Price < 0 || LeadTime < 0 || Warranty < 0 || Technical < 0 || Sustainability < 0 || Total <= 0
            ? DomainErrors.Scoring.InvalidWeights
            : this;

    /// <summary>Returns a copy with the sustainability weight replaced (analyst preference override).</summary>
    public EvaluationCriteria WithSustainability(decimal weight) => this with { Sustainability = weight };
}
