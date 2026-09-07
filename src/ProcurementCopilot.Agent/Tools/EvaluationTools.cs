using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent.Telemetry;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Application.Services;

namespace ProcurementCopilot.Agent.Tools;

/// <summary><c>score_bid</c>: computes the weighted score and persists it to session state.</summary>
public sealed class ScoreBidTool(BidEvaluationService evaluation)
{
    /// <summary>Creates the <see cref="AIFunction"/>.</summary>
    public AIFunction Create() => AIFunctionFactory.Create(ExecuteAsync, ToolNames.ScoreBid);

    /// <summary>Executes the tool.</summary>
    [Description("Score one bid against the RFP's weighted criteria (price, lead time, warranty, technical, sustainability). Relative criteria are normalised against the other bids. Persists the score and returns the breakdown, rank and how many bids are scored so far.")]
    public async Task<string> ExecuteAsync(
        [Description("RFP id in the form RFP-2026-017.")] string rfpId,
        [Description("Bid id in the form BID-001.")] string bidId,
        CancellationToken cancellationToken)
    {
        var rfp = ToolArgumentValidator.RfpId(rfpId);
        var bid = ToolArgumentValidator.BidId(bidId);
        if (rfp.IsFailure || bid.IsFailure)
        {
            return ToolError.FromError(rfp.IsFailure ? rfp.Error : bid.Error);
        }

        using Activity? activity = AgentTelemetry.Source.StartActivity("score_bid");
        activity?.SetTag("procurement.rfp_id", rfp.Value.Value);
        activity?.SetTag("procurement.bid_id", bid.Value.Value);
        var result = await evaluation.ScoreAsync(rfp.Value, bid.Value, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("procurement.outcome", result.IsSuccess ? "ok" : result.Error.Code);
        return result.Match(report => ToolJson.Serialize(report), ToolError.FromError);
    }
}

/// <summary><c>check_vendor_compliance</c>: sanctions list plus required certifications, with a recorded disposition.</summary>
public sealed class CheckVendorComplianceTool(ComplianceService compliance)
{
    /// <summary>Creates the <see cref="AIFunction"/>.</summary>
    public AIFunction Create() => AIFunctionFactory.Create(ExecuteAsync, ToolNames.CheckVendorCompliance);

    /// <summary>Executes the tool.</summary>
    [Description("Check a vendor against the sanctions list and the certifications required by the active RFP. Any hit is recorded with a disposition (Flagged; upgraded to ClarificationRequested when a clarification email is drafted).")]
    public async Task<string> ExecuteAsync(
        [Description("Vendor id in the form VND-0001.")] string vendorId,
        CancellationToken cancellationToken)
    {
        var id = ToolArgumentValidator.VendorId(vendorId);
        if (id.IsFailure)
        {
            return ToolError.FromError(id.Error);
        }

        using Activity? activity = AgentTelemetry.Source.StartActivity("check_vendor_compliance");
        activity?.SetTag("procurement.vendor_id", id.Value.Value);
        var result = await compliance.CheckAsync(id.Value, cancellationToken).ConfigureAwait(false);
        return result.Match(report => ToolJson.Serialize(report), ToolError.FromError);
    }
}
