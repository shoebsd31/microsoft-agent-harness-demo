using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Application.Security;

/// <summary>Validates every tool argument before it reaches a use case. Invalid input becomes a failed <see cref="Result{T}"/>.</summary>
public static class ToolArgumentValidator
{
    /// <summary>Maximum length of any free-text tool argument.</summary>
    public const int MaxTextLength = 4_000;

    /// <summary>Validates an RFP id.</summary>
    public static Result<RfpId> RfpId(string? value) => Domain.ValueObjects.RfpId.Create(value?.Trim());

    /// <summary>Validates a vendor id.</summary>
    public static Result<VendorId> VendorId(string? value) => Domain.ValueObjects.VendorId.Create(value?.Trim());

    /// <summary>Validates a bid id.</summary>
    public static Result<BidId> BidId(string? value) => Domain.ValueObjects.BidId.Create(value?.Trim());

    /// <summary>Validates a currency code against the ISO-4217 allowlist.</summary>
    public static Result<CurrencyCode> Currency(string? value) => CurrencyCode.Create(value);

    /// <summary>Validates a monetary amount: finite, zero or positive, below one billion.</summary>
    public static Result<decimal> Amount(decimal value) =>
        value is >= 0 and <= 1_000_000_000m
            ? value
            : Error.Validation("Amount.OutOfRange", "Amount must be between 0 and 1,000,000,000.");

    /// <summary>Validates free text: required, trimmed and capped at <see cref="MaxTextLength"/> characters.</summary>
    public static Result<string> Text(string? value, string fieldName)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Error.Validation($"{fieldName}.Required", $"{fieldName} is required.");
        }

        return trimmed.Length <= MaxTextLength
            ? trimmed
            : Error.Validation($"{fieldName}.TooLong", $"{fieldName} must be at most {MaxTextLength} characters.");
    }
}
