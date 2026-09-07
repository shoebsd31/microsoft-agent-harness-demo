using ProcurementCopilot.Domain.ValueObjects;

namespace ProcurementCopilot.Domain.Entities;

/// <summary>Lifecycle status of an RFP.</summary>
public enum RfpStatus
{
    /// <summary>Accepting or evaluating bids.</summary>
    Open,

    /// <summary>Awarded or cancelled; no further evaluation.</summary>
    Closed,
}

/// <summary>A request for proposal issued by Contoso Industrial Systems.</summary>
/// <param name="Id">The RFP identifier.</param>
/// <param name="Title">Short title.</param>
/// <param name="Description">What is being procured.</param>
/// <param name="Quantity">Units requested.</param>
/// <param name="Currency">Currency all bids are evaluated in.</param>
/// <param name="Status">Open or closed.</param>
/// <param name="Criteria">Weighted evaluation criteria.</param>
/// <param name="RequiredCertifications">Certifications every vendor must hold.</param>
/// <param name="Category">Procurement category, used to look up required certifications.</param>
public sealed record Rfp(
    RfpId Id,
    string Title,
    string Description,
    int Quantity,
    CurrencyCode Currency,
    RfpStatus Status,
    EvaluationCriteria Criteria,
    IReadOnlyList<string> RequiredCertifications,
    string Category)
{
    /// <summary>Gets a value indicating whether the RFP is still open for evaluation.</summary>
    public bool IsOpen => Status == RfpStatus.Open;
}
