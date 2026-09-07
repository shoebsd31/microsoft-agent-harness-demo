namespace ProcurementCopilot.Domain.Scoring;

/// <summary>Per-criterion normalised scores (0..100) for a single bid.</summary>
/// <param name="Price">Relative price score; the cheapest bid scores 100.</param>
/// <param name="LeadTime">Relative lead-time score; the fastest bid scores 100.</param>
/// <param name="Warranty">Relative warranty score; the longest warranty scores 100.</param>
/// <param name="Technical">Technical compliance percentage, taken as-is.</param>
/// <param name="Sustainability">100 when the vendor holds ISO 14001, otherwise 0.</param>
public sealed record CriterionScores(decimal Price, decimal LeadTime, decimal Warranty, decimal Technical, decimal Sustainability);
