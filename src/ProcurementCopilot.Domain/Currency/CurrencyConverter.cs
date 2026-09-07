using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Errors;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Currency;

/// <summary>Pure currency conversion over a fixed set of seeded <see cref="FxRate"/>s.</summary>
public sealed class CurrencyConverter
{
    private readonly IReadOnlyList<FxRate> _rates;

    /// <summary>Initializes the converter with the available rates.</summary>
    public CurrencyConverter(IReadOnlyList<FxRate> rates) => _rates = rates;

    /// <summary>Finds the rate for a pair, using the inverse rate when only the reverse pair is seeded.</summary>
    public Result<FxRate> FindRate(CurrencyCode from, CurrencyCode to)
    {
        if (from == to)
        {
            return new FxRate(from, to, 1m, DateOnly.MinValue);
        }

        FxRate? direct = _rates.FirstOrDefault(r => r.From == from && r.To == to);
        if (direct is not null)
        {
            return direct;
        }

        FxRate? inverse = _rates.FirstOrDefault(r => r.From == to && r.To == from);
        return inverse is not null && inverse.Rate > 0
            ? new FxRate(from, to, decimal.Round(1m / inverse.Rate, 6), inverse.AsOf)
            : DomainErrors.Money.RateNotFound;
    }

    /// <summary>Converts an amount into the target currency, rounding to two decimals.</summary>
    public Result<Money> Convert(Money amount, CurrencyCode to) =>
        FindRate(amount.Currency, to)
            .Bind(rate => Money.Create(decimal.Round(amount.Amount * rate.Rate, 2, MidpointRounding.AwayFromZero), to));
}
