using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Repositories;

/// <summary>Read access to RFPs.</summary>
public interface IRfpRepository
{
    /// <summary>Returns all RFPs.</summary>
    Task<IReadOnlyList<Rfp>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the RFP with the given id, or <see langword="null"/>.</summary>
    Task<Rfp?> GetByIdAsync(RfpId id, CancellationToken cancellationToken = default);
}
