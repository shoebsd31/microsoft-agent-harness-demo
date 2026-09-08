using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ProcurementCopilot.Application.Abstractions;

namespace ProcurementCopilot.Application.Models;

/// <summary>Source-generated JSON context for every payload that crosses the tool boundary.</summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ToolError))]
[JsonSerializable(typeof(RfpSummary))]
[JsonSerializable(typeof(IReadOnlyList<RfpSummary>))]
[JsonSerializable(typeof(RfpDetail))]
[JsonSerializable(typeof(BidSummary))]
[JsonSerializable(typeof(IReadOnlyList<BidSummary>))]
[JsonSerializable(typeof(VendorProfile))]
[JsonSerializable(typeof(CurrencyConversion))]
[JsonSerializable(typeof(ScoreReport))]
[JsonSerializable(typeof(ComplianceReport))]
[JsonSerializable(typeof(ClarificationDraftReport))]
[JsonSerializable(typeof(AwardRecommendationReport))]
[JsonSerializable(typeof(EvaluationProgress))]
[JsonSerializable(typeof(QueryToolResult))]
[JsonSerializable(typeof(EvaluationState))]
[JsonSerializable(typeof(EmailDraft))]
[JsonSerializable(typeof(ApprovalRecord))]
[JsonSerializable(typeof(ActionRecord))]
[JsonSerializable(typeof(SessionSummary))]
public sealed partial class ToolJsonContext : JsonSerializerContext
{
    /// <summary>Serialises a payload with the shared options.</summary>
    public static string Serialize<T>(T value, JsonTypeInfo<T> typeInfo) => JsonSerializer.Serialize(value, typeInfo);
}
