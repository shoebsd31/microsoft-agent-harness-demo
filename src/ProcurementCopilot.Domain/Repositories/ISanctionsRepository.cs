using ProcurementCopilot.Domain.Entities;

namespace ProcurementCopilot.Domain.Repositories;

/// <summary>Read access to the sanctions list.</summary>
public interface ISanctionsRepository
{
    /// <summary>Returns every sanctions entry.</summary>
    Task<IReadOnlyList<SanctionsEntry>> GetAllAsync(CancellationToken cancellationToken = default);
}
