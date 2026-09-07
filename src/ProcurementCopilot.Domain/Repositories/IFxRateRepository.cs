using ProcurementCopilot.Domain.Entities;

namespace ProcurementCopilot.Domain.Repositories;

/// <summary>Read access to seeded exchange rates.</summary>
public interface IFxRateRepository
{
    /// <summary>Returns every seeded rate.</summary>
    Task<IReadOnlyList<FxRate>> GetAllAsync(CancellationToken cancellationToken = default);
}
