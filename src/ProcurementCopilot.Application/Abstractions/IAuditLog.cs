namespace ProcurementCopilot.Application.Abstractions;

/// <summary>A human approval decision.</summary>
/// <param name="At">When the decision was made.</param>
/// <param name="Who">Who decided (OS user name).</param>
/// <param name="Tool">Tool name.</param>
/// <param name="ArgumentsHash">SHA-256 of the canonical arguments.</param>
/// <param name="Decision">approve-once, approve-always or deny.</param>
/// <param name="SessionId">Session in which the decision was made.</param>
public sealed record ApprovalRecord(DateTimeOffset At, string Who, string Tool, string ArgumentsHash, string Decision, Guid SessionId);

/// <summary>A side-effecting tool invocation.</summary>
/// <param name="At">When the action happened.</param>
/// <param name="Tool">Tool name.</param>
/// <param name="ArgumentsHash">SHA-256 of the canonical arguments.</param>
/// <param name="Outcome">Short outcome such as <c>ok</c>, <c>blocked</c> or an error code.</param>
/// <param name="Detail">Optional detail (never contains secrets).</param>
public sealed record ActionRecord(DateTimeOffset At, string Tool, string ArgumentsHash, string Outcome, string? Detail);

/// <summary>Append-only audit trail (JSON lines) under <c>workspace/output/audit</c>.</summary>
public interface IAuditLog
{
    /// <summary>Appends an approval decision.</summary>
    Task RecordApprovalAsync(ApprovalRecord record, CancellationToken cancellationToken = default);

    /// <summary>Appends a side-effecting action.</summary>
    Task RecordActionAsync(ActionRecord record, CancellationToken cancellationToken = default);
}
