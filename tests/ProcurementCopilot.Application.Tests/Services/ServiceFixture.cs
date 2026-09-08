using ProcurementCopilot.Application.Services;
using ProcurementCopilot.Infrastructure.Data;
using ProcurementCopilot.Infrastructure.Outbox;
using ProcurementCopilot.Infrastructure.Repositories;
using ProcurementCopilot.Testing.Fakes;

namespace ProcurementCopilot.Application.Tests.Services;

/// <summary>Real seed data + in-memory fakes for the use-case services.</summary>
public sealed class ServiceFixture
{
    public ServiceFixture()
    {
        var store = new SeedDataStore(TestPaths.DataDirectory());
        Rfps = new JsonRfpRepository(store);
        Vendors = new JsonVendorRepository(store);
        Bids = new JsonBidRepository(store);
        Sanctions = new CsvSanctionsRepository(store);
        Currency = new CurrencyService(new JsonFxRateRepository(store));
        Queries = new RfpQueryService(Rfps, Bids, Vendors, State);
        Evaluation = new BidEvaluationService(Rfps, Bids, Vendors, Currency, State);
        Compliance = new ComplianceService(Vendors, Rfps, Sanctions, State);
        Clarifications = new ClarificationService(Vendors, Outbox, Audit, State, Clock);
        Awards = new AwardService(Rfps, Vendors, Bids, Sanctions, Currency, new FileAwardRecorder(Outbox), Audit, State, Clock);
    }

    public InMemoryEvaluationStateStore State { get; } = new();

    public InMemoryOutbox Outbox { get; } = new();

    public InMemoryAuditLog Audit { get; } = new();

    public FixedClock Clock { get; } = new(new DateTimeOffset(2026, 9, 7, 8, 0, 0, TimeSpan.Zero));

    public JsonRfpRepository Rfps { get; }

    public JsonVendorRepository Vendors { get; }

    public JsonBidRepository Bids { get; }

    public CsvSanctionsRepository Sanctions { get; }

    public CurrencyService Currency { get; }

    public RfpQueryService Queries { get; }

    public BidEvaluationService Evaluation { get; }

    public ComplianceService Compliance { get; }

    public ClarificationService Clarifications { get; }

    public AwardService Awards { get; }
}
