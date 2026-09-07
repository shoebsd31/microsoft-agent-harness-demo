namespace ProcurementCopilot.Application.Abstractions;

/// <summary>Testable source of the current time.</summary>
public interface IClock
{
    /// <summary>Gets the current UTC time.</summary>
    DateTimeOffset UtcNow { get; }
}
