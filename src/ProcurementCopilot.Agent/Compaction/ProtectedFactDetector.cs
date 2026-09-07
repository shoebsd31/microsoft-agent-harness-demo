using System.Text.RegularExpressions;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using ProcurementCopilot.Agent.Tools;

namespace ProcurementCopilot.Agent.Compaction;

/// <summary>Identifies message groups and facts that compaction must preserve verbatim.</summary>
public static partial class ProtectedFactDetector
{
    private static readonly HashSet<string> ProtectedTools = new(StringComparer.Ordinal)
    {
        ToolNames.ScoreBid, ToolNames.RecordAwardRecommendation, ToolNames.DraftClarificationEmail,
    };

    /// <summary>A group is protected when it holds a score, an award, a clarification or an approval decision.</summary>
    public static bool IsProtected(CompactionMessageGroup group) =>
        group.Kind == CompactionGroupKind.System ||
        group.Kind == CompactionGroupKind.Summary ||
        group.Messages.SelectMany(m => m.Contents).Any(IsProtectedContent);

    /// <summary>Returns the RFP ids mentioned in the text of a group.</summary>
    public static IEnumerable<string> RfpIds(CompactionMessageGroup group) =>
        group.Messages.Select(m => m.Text).Where(t => !string.IsNullOrEmpty(t)).SelectMany(t => RfpIdPattern().Matches(t).Select(m => m.Value)).Distinct(StringComparer.Ordinal);

    /// <summary>Returns a one-line digest of the tool calls in a group, for the summary.</summary>
    public static IEnumerable<string> ToolCallDigest(CompactionMessageGroup group) =>
        group.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>()
            .Select(c => c.Arguments is { Count: > 0 } ? $"{c.Name}({string.Join(", ", c.Arguments.Values.Select(v => v?.ToString()))})" : $"{c.Name}()");

    private static bool IsProtectedContent(AIContent content) => content switch
    {
        FunctionCallContent call => ProtectedTools.Contains(call.Name),
        ToolApprovalRequestContent => true,
        ToolApprovalResponseContent => true,
        _ => false,
    };

    [GeneratedRegex(@"\b[A-Z]{3}-\d{4}-\d{3}\b")]
    private static partial Regex RfpIdPattern();
}
