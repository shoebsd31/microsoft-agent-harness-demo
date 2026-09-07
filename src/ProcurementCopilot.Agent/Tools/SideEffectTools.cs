using System.ComponentModel;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Application.Services;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Agent.Tools;

/// <summary><c>draft_clarification_email</c>: writes a draft to the outbox. Requires approval and execute mode.</summary>
public sealed class DraftClarificationEmailTool(ClarificationService clarifications)
{
    /// <summary>Creates the <see cref="AIFunction"/> (unwrapped; the toolset adds the mode guard and approval wrapper).</summary>
    public AIFunction Create() => AIFunctionFactory.Create(ExecuteAsync, ToolNames.DraftClarificationEmail);

    /// <summary>Executes the tool.</summary>
    [Description("Draft a clarification email to a vendor about an ambiguous or missing item in its bid. The draft is written to the workspace outbox; nothing is sent. Requires analyst approval and execute mode.")]
    public async Task<string> ExecuteAsync(
        [Description("Vendor id in the form VND-0001.")] string vendorId,
        [Description("Email subject (max 4000 characters).")] string subject,
        [Description("Email body (max 4000 characters).")] string body,
        CancellationToken cancellationToken)
    {
        var id = ToolArgumentValidator.VendorId(vendorId);
        var subjectResult = ToolArgumentValidator.Text(subject, "subject");
        var bodyResult = ToolArgumentValidator.Text(body, "body");
        Error? error = id.IsFailure ? id.Error : subjectResult.IsFailure ? subjectResult.Error : bodyResult.IsFailure ? bodyResult.Error : null;
        if (error is not null)
        {
            return ToolError.FromError(error);
        }

        var result = await clarifications.DraftAsync(id.Value, subjectResult.Value, bodyResult.Value, cancellationToken).ConfigureAwait(false);
        return result.Match(report => ToolJson.Serialize(report), ToolError.FromError);
    }
}

/// <summary><c>record_award_recommendation</c>: writes <c>award-recommendation.json</c> and audits. Requires approval and execute mode.</summary>
public sealed class RecordAwardRecommendationTool(AwardService awards)
{
    /// <summary>Creates the <see cref="AIFunction"/> (unwrapped; the toolset adds the mode guard and approval wrapper).</summary>
    public AIFunction Create() => AIFunctionFactory.Create(ExecuteAsync, ToolNames.RecordAwardRecommendation);

    /// <summary>Executes the tool.</summary>
    [Description("Record the award recommendation for an RFP: the recommended vendor and the rationale. Writes output/award-recommendation.json and an audit entry. Sanctioned vendors are refused. Requires analyst approval and execute mode.")]
    public async Task<string> ExecuteAsync(
        [Description("RFP id in the form RFP-2026-017.")] string rfpId,
        [Description("Recommended vendor id in the form VND-0001.")] string vendorId,
        [Description("Rationale citing the scores and compliance results (max 4000 characters).")] string rationale,
        CancellationToken cancellationToken)
    {
        var rfp = ToolArgumentValidator.RfpId(rfpId);
        var vendor = ToolArgumentValidator.VendorId(vendorId);
        var text = ToolArgumentValidator.Text(rationale, "rationale");
        Error? error = rfp.IsFailure ? rfp.Error : vendor.IsFailure ? vendor.Error : text.IsFailure ? text.Error : null;
        if (error is not null)
        {
            return ToolError.FromError(error);
        }

        var result = await awards.RecordAsync(rfp.Value, vendor.Value, text.Value, cancellationToken).ConfigureAwait(false);
        return result.Match(report => ToolJson.Serialize(report), ToolError.FromError);
    }
}
