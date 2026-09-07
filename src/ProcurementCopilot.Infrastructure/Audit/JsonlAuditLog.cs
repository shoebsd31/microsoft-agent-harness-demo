using System.Text.Json;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Models;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Infrastructure.Audit;

/// <summary>Append-only JSON-lines audit trail under <c>workspace/output/audit</c>, confined by <see cref="WorkspacePathPolicy"/>.</summary>
public sealed class JsonlAuditLog : IAuditLog
{
    private const string ApprovalsFile = "output/audit/approvals.jsonl";
    private const string ActionsFile = "output/audit/actions.jsonl";

    private readonly WorkspacePathPolicy _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes the log.</summary>
    public JsonlAuditLog(WorkspacePathPolicy paths) => _paths = paths;

    /// <inheritdoc />
    public Task RecordApprovalAsync(ApprovalRecord record, CancellationToken cancellationToken = default) =>
        AppendAsync(ApprovalsFile, JsonSerializer.Serialize(record, ToolJsonContext.Default.ApprovalRecord), cancellationToken);

    /// <inheritdoc />
    public Task RecordActionAsync(ActionRecord record, CancellationToken cancellationToken = default) =>
        AppendAsync(ActionsFile, JsonSerializer.Serialize(record, ToolJsonContext.Default.ActionRecord), cancellationToken);

    private async Task AppendAsync(string relativePath, string line, CancellationToken cancellationToken)
    {
        Result<string> resolved = _paths.Resolve(relativePath, PathAccess.Write);
        if (resolved.IsFailure)
        {
            throw new InvalidOperationException($"Audit path rejected by workspace policy: {resolved.Error}");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(resolved.Value)!);
            await File.AppendAllTextAsync(resolved.Value, line + Environment.NewLine, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
