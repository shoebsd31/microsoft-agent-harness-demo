using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Repositories;

/// <summary>Read access to vendors.</summary>
public interface IVendorRepository
{
    /// <summary>Returns all vendors.</summary>
    Task<IReadOnlyList<Vendor>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the vendor with the given id, or <see langword="null"/>.</summary>
    Task<Vendor?> GetByIdAsync(VendorId id, CancellationToken cancellationToken = default);
}
