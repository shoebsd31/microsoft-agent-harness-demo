using ProcurementCopilot.Application.Abstractions;

namespace ProcurementCopilot.Infrastructure.Time;

/// <summary>Wall-clock implementation of <see cref="IClock"/>.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
