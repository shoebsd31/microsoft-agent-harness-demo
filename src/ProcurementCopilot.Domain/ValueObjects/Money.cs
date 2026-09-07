using System.Globalization;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Errors;

namespace ProcurementCopilot.Domain.ValueObjects;

/// <summary>An immutable amount in a specific currency.</summary>
public sealed record Money
{
    private Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Gets the amount.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the currency.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>Creates a <see cref="Money"/> value; amounts must be zero or positive.</summary>
    public static Result<Money> Create(decimal amount, CurrencyCode currency) =>
        amount < 0 ? DomainErrors.Money.InvalidAmount : new Money(amount, currency);

    /// <summary>Creates a <see cref="Money"/> value from a raw currency code.</summary>
    public static Result<Money> Create(decimal amount, string currencyCode) =>
        CurrencyCode.Create(currencyCode).Bind(currency => Create(amount, currency));

    /// <summary>Multiplies the amount by a factor, keeping the currency.</summary>
    public Money Times(decimal factor) => new(Amount * factor, Currency);

    /// <summary>Adds another amount in the same currency.</summary>
    public Result<Money> Add(Money other) =>
        other.Currency == Currency
            ? new Money(Amount + other.Amount, Currency)
            : DomainErrors.Money.CurrencyMismatch;

    /// <summary>Formats as <c>1 234.56 EUR</c> with two decimals.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:N2} {Currency.Value}");
}
