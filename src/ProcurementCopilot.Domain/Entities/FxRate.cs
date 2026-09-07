using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Entities;

/// <summary>A fixed, dated exchange rate: one unit of <paramref name="From"/> buys <paramref name="Rate"/> units of <paramref name="To"/>.</summary>
/// <param name="From">Source currency.</param>
/// <param name="To">Target currency.</param>
/// <param name="Rate">Multiplier applied to the source amount.</param>
/// <param name="AsOf">Date the rate was fixed.</param>
public sealed record FxRate(CurrencyCode From, CurrencyCode To, decimal Rate, DateOnly AsOf);
