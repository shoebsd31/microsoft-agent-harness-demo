namespace ProcurementCopilot.Application.Abstractions;

/// <summary>How a compliance hit has been dealt with.</summary>
public enum ComplianceDisposition
{
    /// <summary>Hit recorded and flagged for the analyst.</summary>
    Flagged,

    /// <summary>A clarification request was drafted for the vendor.</summary>
    ClarificationRequested,
}

/// <summary>A persisted score entry.</summary>
public sealed class PersistedScore
{
    /// <summary>Bid id.</summary>
    public string BidId { get; set; } = string.Empty;

    /// <summary>Vendor id.</summary>
    public string VendorId { get; set; } = string.Empty;

    /// <summary>Weighted total.</summary>
    public decimal WeightedTotal { get; set; }
}

/// <summary>A persisted compliance hit.</summary>
public sealed class PersistedComplianceHit
{
    /// <summary>Vendor id.</summary>
    public string VendorId { get; set; } = string.Empty;

    /// <summary>Verdict text.</summary>
    public string Verdict { get; set; } = string.Empty;

    /// <summary>Disposition.</summary>
    public ComplianceDisposition Disposition { get; set; }
}

/// <summary>A persisted approval decision.</summary>
public sealed class PersistedApproval
{
    /// <summary>Tool name.</summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>Decision text.</summary>
    public string Decision { get; set; } = string.Empty;
}

/// <summary>Evaluation progress for the active RFP, stored in the agent session.</summary>
public sealed class EvaluationState
{
    /// <summary>The RFP under evaluation, or <see langword="null"/>.</summary>
    public string? ActiveRfpId { get; set; }

    /// <summary>Scores recorded so far, keyed by bid id.</summary>
    public Dictionary<string, PersistedScore> Scores { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Compliance hits, keyed by vendor id.</summary>
    public Dictionary<string, PersistedComplianceHit> ComplianceHits { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Approval decisions taken in the session.</summary>
    public List<PersistedApproval> Approvals { get; set; } = [];

    /// <summary>Vendors with an open clarification request.</summary>
    public List<string> OpenClarifications { get; set; } = [];

    /// <summary>Vendor id of the recorded award recommendation, if any.</summary>
    public string? AwardedVendorId { get; set; }
}

/// <summary>Reads and writes the <see cref="EvaluationState"/> of the running session.</summary>
public interface IEvaluationStateStore
{
    /// <summary>Returns the current state (a new one when none exists).</summary>
    EvaluationState Get();

    /// <summary>Persists the state.</summary>
    void Save(EvaluationState state);
}
