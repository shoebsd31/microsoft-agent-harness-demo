namespace ProcurementCopilot.Domain.Common;

/// <summary>
/// A typed, non-throwing description of why an operation failed.
/// </summary>
/// <param name="Code">Stable machine-readable code such as <c>Bid.NotFound</c>.</param>
/// <param name="Message">Human-readable explanation safe to show to the model and the analyst.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>Represents the absence of an error.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>Creates a validation error with the given code and message.</summary>
    public static Error Validation(string code, string message) => new(code, message);

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";
}
