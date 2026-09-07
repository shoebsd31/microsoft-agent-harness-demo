using System.Globalization;
using System.Text.Json;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.Errors;
using ProcurementCopilot.Domain.Repositories;
using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Application.Services;

/// <summary>Records an award recommendation (never an award) to the workspace and the audit trail.</summary>
public sealed class AwardService
{
    /// <summary>Tool name used in the audit trail.</summary>
    public const string ToolName = "record_award_recommendation";

    private readonly IRfpRepository _rfps;
    private readonly IVendorRepository _vendors;
    private readonly ISanctionsRepository _sanctions;
    private readonly IOutbox _outbox;
    private readonly IAuditLog _audit;
    private readonly IEvaluationStateStore _state;
    private readonly IClock _clock;

    /// <summary>Initializes the service.</summary>
    public AwardService(IRfpRepository rfps, IVendorRepository vendors, ISanctionsRepository sanctions, IOutbox outbox, IAuditLog audit, IEvaluationStateStore state, IClock clock)
    {
        _rfps = rfps;
        _vendors = vendors;
        _sanctions = sanctions;
        _outbox = outbox;
        _audit = audit;
        _state = state;
        _clock = clock;
    }

    /// <summary>Validates the recommendation, refuses sanctioned vendors, writes <c>award-recommendation.json</c> and audits.</summary>
    public async Task<Result<AwardRecommendationReport>> RecordAsync(RfpId rfpId, VendorId vendorId, string rationale, CancellationToken cancellationToken = default)
    {
        if (await _rfps.GetByIdAsync(rfpId, cancellationToken).ConfigureAwait(false) is null)
        {
            return DomainErrors.NotFound.Rfp;
        }

        Vendor? vendor = await _vendors.GetByIdAsync(vendorId, cancellationToken).ConfigureAwait(false);
        if (vendor is null)
        {
            return DomainErrors.NotFound.Vendor;
        }

        IReadOnlyList<SanctionsEntry> sanctions = await _sanctions.GetAllAsync(cancellationToken).ConfigureAwait(false);
        string hash = ArgumentHasher.Hash(rfpId.Value, vendorId.Value, rationale);
        if (sanctions.Any(s => s.VendorId == vendor.Id))
        {
            await _audit.RecordActionAsync(new ActionRecord(_clock.UtcNow, ToolName, hash, "blocked", DomainErrors.Award.VendorSanctioned.Code), cancellationToken).ConfigureAwait(false);
            return DomainErrors.Award.VendorSanctioned;
        }

        EvaluationState state = _state.Get();
        decimal? winning = state.Scores.Values.FirstOrDefault(s => string.Equals(s.VendorId, vendorId.Value, StringComparison.Ordinal))?.WeightedTotal;
        DateTimeOffset now = _clock.UtcNow;
        var document = new AwardRecommendationDocument(rfpId.Value, vendorId.Value, vendor.Name, winning, rationale, now.ToString("O", CultureInfo.InvariantCulture));
        string savedTo = await _outbox.SaveDocumentAsync("award-recommendation.json",
            JsonSerializer.Serialize(document, AwardJsonContext.Default.AwardRecommendationDocument), cancellationToken).ConfigureAwait(false);

        await _audit.RecordActionAsync(new ActionRecord(now, ToolName, hash, "ok", savedTo), cancellationToken).ConfigureAwait(false);
        state.AwardedVendorId = vendorId.Value;
        _state.Save(state);
        return new AwardRecommendationReport(rfpId.Value, vendorId.Value, winning, savedTo, now.ToString("O", CultureInfo.InvariantCulture));
    }
}

/// <summary>The persisted award recommendation document.</summary>
public sealed record AwardRecommendationDocument(string RfpId, string VendorId, string VendorName, decimal? WinningScore, string Rationale, string RecordedAt);

/// <summary>JSON context for the award document.</summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
[System.Text.Json.Serialization.JsonSerializable(typeof(AwardRecommendationDocument))]
public sealed partial class AwardJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
