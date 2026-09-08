using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Infrastructure.Outbox;

/// <summary>Records the award recommendation as <c>output/award-recommendation.json</c> in the workspace.</summary>
public sealed class FileAwardRecorder : IAwardRecorder
{
    private readonly IOutbox _outbox;

    /// <summary>Initializes the recorder.</summary>
    public FileAwardRecorder(IOutbox outbox) => _outbox = outbox;

    /// <inheritdoc />
    public async Task<Result<AwardRecordReference>> RecordAsync(AwardRecordRequest request, CancellationToken cancellationToken = default)
    {
        var document = new AwardRecommendationDocument(request.RfpId.Value, request.VendorId.Value, request.VendorName, request.WinningScore,
            request.Rationale, request.UnitPriceInRfpCurrency, request.LeadTimeWeeks, request.RecordedAt.ToString("O", CultureInfo.InvariantCulture));
        string savedTo = await _outbox.SaveDocumentAsync("award-recommendation.json",
            JsonSerializer.Serialize(document, AwardJsonContext.Default.AwardRecommendationDocument), cancellationToken).ConfigureAwait(false);
        return new AwardRecordReference(savedTo, null);
    }
}

/// <summary>The persisted award recommendation document.</summary>
public sealed record AwardRecommendationDocument(string RfpId, string VendorId, string VendorName, decimal? WinningScore, string Rationale, decimal UnitPriceInRfpCurrency, int LeadTimeWeeks, string RecordedAt);

/// <summary>JSON context for the award document.</summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AwardRecommendationDocument))]
public sealed partial class AwardJsonContext : JsonSerializerContext;
