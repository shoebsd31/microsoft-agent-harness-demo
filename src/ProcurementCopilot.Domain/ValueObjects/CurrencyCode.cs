using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Errors;

namespace ProcurementCopilot.Domain.ValueObjects;

/// <summary>An ISO-4217 currency code restricted to the allowlist used by the demo.</summary>
public sealed record CurrencyCode
{
    /// <summary>Currencies accepted by every tool and value object.</summary>
    public static readonly IReadOnlySet<string> Allowed =
        new HashSet<string>(StringComparer.Ordinal) { "EUR", "USD", "GBP", "JPY", "CHF", "SEK", "CAD", "AUD", "MXN" };

    /// <summary>The euro.</summary>
    public static readonly CurrencyCode Eur = new("EUR");

    /// <summary>The US dollar.</summary>
    public static readonly CurrencyCode Usd = new("USD");

    private CurrencyCode(string value) => Value = value;

    /// <summary>Gets the upper-case three-letter code.</summary>
    public string Value { get; }

    /// <summary>Validates and creates a <see cref="CurrencyCode"/>; input is trimmed and upper-cased.</summary>
    public static Result<CurrencyCode> Create(string? value)
    {
        string normalised = value?.Trim().ToUpperInvariant() ?? string.Empty;
        return Allowed.Contains(normalised) ? new CurrencyCode(normalised) : DomainErrors.Money.InvalidCurrency;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
