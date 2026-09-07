using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Domain.ValueObjects;
using ProcurementCopilot.Infrastructure.Data;

namespace ProcurementCopilot.Infrastructure.Repositories;

/// <summary>RFP repository over the seed files.</summary>
public sealed class JsonRfpRepository(SeedDataStore store) : IRfpRepository
{
    /// <inheritdoc />
    public Task<IReadOnlyList<Rfp>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(store.Data.Rfps);

    /// <inheritdoc />
    public Task<Rfp?> GetByIdAsync(RfpId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Data.Rfps.FirstOrDefault(r => r.Id == id));
}

/// <summary>Vendor repository over the seed files.</summary>
public sealed class JsonVendorRepository(SeedDataStore store) : IVendorRepository
{
    /// <inheritdoc />
    public Task<IReadOnlyList<Vendor>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(store.Data.Vendors);

    /// <inheritdoc />
    public Task<Vendor?> GetByIdAsync(VendorId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Data.Vendors.FirstOrDefault(v => v.Id == id));
}

/// <summary>Bid repository over the seed files.</summary>
public sealed class JsonBidRepository(SeedDataStore store) : IBidRepository
{
    /// <inheritdoc />
    public Task<IReadOnlyList<Bid>> GetByRfpAsync(RfpId rfpId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Bid>>(store.Data.Bids.Where(b => b.RfpId == rfpId).ToList());

    /// <inheritdoc />
    public Task<Bid?> GetByIdAsync(BidId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Data.Bids.FirstOrDefault(b => b.Id == id));
}

/// <summary>Sanctions repository over <c>sanctions.csv</c>.</summary>
public sealed class CsvSanctionsRepository(SeedDataStore store) : ISanctionsRepository
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SanctionsEntry>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(store.Data.Sanctions);
}

/// <summary>FX-rate repository over <c>fx-rates.json</c>.</summary>
public sealed class JsonFxRateRepository(SeedDataStore store) : IFxRateRepository
{
    /// <inheritdoc />
    public Task<IReadOnlyList<FxRate>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(store.Data.FxRates);
}
