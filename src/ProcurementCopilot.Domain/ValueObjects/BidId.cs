using System.Text.RegularExpressions;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Errors;

namespace ProcurementCopilot.Domain.ValueObjects;

/// <summary>Strongly typed bid identifier such as <c>BID-001</c>.</summary>
public sealed partial record BidId
{
    private BidId(string value) => Value = value;

    /// <summary>Gets the canonical identifier string.</summary>
    public string Value { get; }

    /// <summary>Validates and creates a <see cref="BidId"/>.</summary>
    public static Result<BidId> Create(string? value) =>
        value is not null && Pattern().IsMatch(value)
            ? new BidId(value)
            : DomainErrors.Ids.InvalidBidId;

    /// <summary>Returns <see langword="true"/> when the value has a valid format.</summary>
    public static bool IsValid(string? value) => value is not null && Pattern().IsMatch(value);

    /// <inheritdoc />
    public override string ToString() => Value;

    [GeneratedRegex(@"^BID-\d{3}$")]
    private static partial Regex Pattern();
}
