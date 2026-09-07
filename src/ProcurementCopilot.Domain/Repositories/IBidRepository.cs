using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Repositories;

/// <summary>Read access to bids.</summary>
public interface IBidRepository
{
    /// <summary>Returns the bids submitted for an RFP.</summary>
    Task<IReadOnlyList<Bid>> GetByRfpAsync(RfpId rfpId, CancellationToken cancellationToken = default);

    /// <summary>Returns the bid with the given id, or <see langword="null"/>.</summary>
    Task<Bid?> GetByIdAsync(BidId id, CancellationToken cancellationToken = default);
}
