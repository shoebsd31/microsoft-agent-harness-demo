using System.Globalization;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Currency;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Application.Services;

/// <summary>Currency conversion over the seeded rates.</summary>
public sealed class CurrencyService
{
    private readonly IFxRateRepository _rates;

    /// <summary>Initializes the service.</summary>
    public CurrencyService(IFxRateRepository rates) => _rates = rates;

    /// <summary>Builds a converter over all seeded rates.</summary>
    public async Task<CurrencyConverter> GetConverterAsync(CancellationToken cancellationToken = default) =>
        new(await _rates.GetAllAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Converts an amount between two currencies.</summary>
    public async Task<Result<CurrencyConversion>> ConvertAsync(decimal amount, CurrencyCode from, CurrencyCode to, CancellationToken cancellationToken = default)
    {
        CurrencyConverter converter = await GetConverterAsync(cancellationToken).ConfigureAwait(false);
        Result<FxRate> rate = converter.FindRate(from, to);
        if (rate.IsFailure)
        {
            return rate.Error;
        }

        return Money.Create(amount, from)
            .Bind(money => converter.Convert(money, to))
            .Map(converted => new CurrencyConversion(amount, from.Value, converted.Amount, to.Value, rate.Value.Rate,
                rate.Value.AsOf == DateOnly.MinValue ? "n/a" : rate.Value.AsOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "data/fx-rates.json (seeded, fixed)"));
    }
}
