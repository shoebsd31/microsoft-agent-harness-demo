namespace ProcurementCopilot.Agent.Tools;

/// <summary>Model-facing tool names. Side-effecting tools are listed in <c>Security:ApprovalPolicy:RequireApprovalFor</c>.</summary>
public static class ToolNames
{
    /// <summary>Lists open RFPs.</summary>
    public const string ListOpenRfps = "list_open_rfps";

    /// <summary>Returns RFP detail.</summary>
    public const string GetRfp = "get_rfp";

    /// <summary>Lists the bids of an RFP.</summary>
    public const string ListBids = "list_bids";

    /// <summary>Returns a vendor profile.</summary>
    public const string GetVendorProfile = "get_vendor_profile";

    /// <summary>Converts currency with seeded rates.</summary>
    public const string ConvertCurrency = "convert_currency";

    /// <summary>Scores a bid.</summary>
    public const string ScoreBid = "score_bid";

    /// <summary>Checks vendor compliance.</summary>
    public const string CheckVendorCompliance = "check_vendor_compliance";

    /// <summary>Drafts a clarification email (side effect).</summary>
    public const string DraftClarificationEmail = "draft_clarification_email";

    /// <summary>Records an award recommendation (side effect).</summary>
    public const string RecordAwardRecommendation = "record_award_recommendation";

    /// <summary>Confined shell (side effect: process execution).</summary>
    public const string Shell = "shell";

    /// <summary>Tools with side effects that the mode guard blocks in plan mode.</summary>
    public static readonly IReadOnlySet<string> SideEffecting =
        new HashSet<string>(StringComparer.Ordinal) { DraftClarificationEmail, RecordAwardRecommendation };

    /// <summary>Read-only tools.</summary>
    public static readonly IReadOnlySet<string> ReadOnly =
        new HashSet<string>(StringComparer.Ordinal) { ListOpenRfps, GetRfp, ListBids, GetVendorProfile, ConvertCurrency, ScoreBid, CheckVendorCompliance };
}
