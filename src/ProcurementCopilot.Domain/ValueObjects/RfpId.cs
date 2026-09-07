using System.Text.RegularExpressions;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Errors;

namespace ProcurementCopilot.Domain.ValueObjects;

/// <summary>Strongly typed RFP identifier such as <c>RFP-2026-017</c>.</summary>
public sealed partial record RfpId
{
    private RfpId(string value) => Value = value;

    /// <summary>Gets the canonical identifier string.</summary>
    public string Value { get; }

    /// <summary>Validates and creates an <see cref="RfpId"/>.</summary>
    public static Result<RfpId> Create(string? value) =>
        value is not null && Pattern().IsMatch(value)
            ? new RfpId(value)
            : DomainErrors.Ids.InvalidRfpId;

    /// <summary>Returns <see langword="true"/> when the value has a valid format.</summary>
    public static bool IsValid(string? value) => value is not null && Pattern().IsMatch(value);

    /// <inheritdoc />
    public override string ToString() => Value;

    [GeneratedRegex(@"^[A-Z]{3}-\d{4}-\d{3}$")]
    private static partial Regex Pattern();
}
