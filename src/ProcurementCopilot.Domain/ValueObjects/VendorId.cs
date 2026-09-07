using System.Text.RegularExpressions;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Errors;

namespace ProcurementCopilot.Domain.ValueObjects;

/// <summary>Strongly typed vendor identifier such as <c>VND-0001</c>.</summary>
public sealed partial record VendorId
{
    private VendorId(string value) => Value = value;

    /// <summary>Gets the canonical identifier string.</summary>
    public string Value { get; }

    /// <summary>Validates and creates a <see cref="VendorId"/>.</summary>
    public static Result<VendorId> Create(string? value) =>
        value is not null && Pattern().IsMatch(value)
            ? new VendorId(value)
            : DomainErrors.Ids.InvalidVendorId;

    /// <summary>Returns <see langword="true"/> when the value has a valid format.</summary>
    public static bool IsValid(string? value) => value is not null && Pattern().IsMatch(value);

    /// <inheritdoc />
    public override string ToString() => Value;

    [GeneratedRegex(@"^VND-\d{4}$")]
    private static partial Regex Pattern();
}
