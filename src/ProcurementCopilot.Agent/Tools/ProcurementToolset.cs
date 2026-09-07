using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent.Middleware;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Security;

namespace ProcurementCopilot.Agent.Tools;

/// <summary>
/// Assembles the procurement tools. Read-only tools are plain functions; side-effecting tools get the mode guard
/// (inner) and the <see cref="ApprovalRequiredAIFunction"/> wrapper (outer) driven by <see cref="ApprovalPolicy"/>.
/// </summary>
public sealed class ProcurementToolset
{
    private readonly Dictionary<string, AIFunction> _raw;
    private readonly ApprovalPolicy _approvals;
    private readonly IAgentModeAccessor _modes;

    /// <summary>Initializes the toolset from the individual tool classes.</summary>
    public ProcurementToolset(
        ListOpenRfpsTool listOpenRfps, GetRfpTool getRfp, ListBidsTool listBids, GetVendorProfileTool getVendorProfile,
        ConvertCurrencyTool convertCurrency, ScoreBidTool scoreBid, CheckVendorComplianceTool checkCompliance,
        DraftClarificationEmailTool draftEmail, RecordAwardRecommendationTool recordAward,
        ApprovalPolicy approvals, IAgentModeAccessor modes)
    {
        _approvals = approvals;
        _modes = modes;
        _raw = new AIFunction[]
        {
            listOpenRfps.Create(), getRfp.Create(), listBids.Create(), getVendorProfile.Create(), convertCurrency.Create(),
            scoreBid.Create(), checkCompliance.Create(), draftEmail.Create(), recordAward.Create(),
        }.ToDictionary(f => f.Name, StringComparer.Ordinal);
    }

    /// <summary>Gets the raw (unwrapped) read-only function by name, for narrow child-agent tool sets.</summary>
    public AIFunction Get(string name)
    {
        if (!ToolNames.ReadOnly.Contains(name))
        {
            throw new InvalidOperationException($"'{name}' is not a read-only tool and cannot be shared with child agents.");
        }

        return _raw[name];
    }

    /// <summary>Read-only tools, unwrapped.</summary>
    public IReadOnlyList<AIFunction> ReadOnlyTools => _raw.Values.Where(f => ToolNames.ReadOnly.Contains(f.Name)).ToList();

    /// <summary>Side-effecting tools, wrapped with the mode guard and, when the policy says so, the approval wrapper.</summary>
    public IReadOnlyList<AIFunction> SideEffectingTools =>
        _raw.Values.Where(f => ToolNames.SideEffecting.Contains(f.Name)).Select(Wrap).ToList();

    /// <summary>Every tool for the main agent, wrapped according to policy.</summary>
    public IReadOnlyList<AITool> AllTools => [.. ReadOnlyTools, .. SideEffectingTools];

    /// <summary>Applies policy wrapping to any function (used for the shell tool too).</summary>
    public AIFunction Wrap(AIFunction function)
    {
        AIFunction guarded = ModeGuardMiddleware.Apply(function, _modes, ToolNames.SideEffecting);
        return _approvals.RequiresApproval(function.Name) ? new ApprovalRequiredAIFunction(guarded) : guarded;
    }
}
