using System.Text;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ProcurementCopilot.Agent.Compaction;

/// <summary>
/// Custom compaction: when the included token count exceeds the budget, the oldest unprotected groups are excluded
/// and replaced by one deterministic summary group. Groups holding the RFP id, recorded bid scores, approval decisions
/// and open clarifications are never excluded. No model call is made.
/// </summary>
public sealed class ProcurementCompactionStrategy : CompactionStrategy
{
    /// <summary>Number of most-recent groups that are always kept.</summary>
    public const int MinimumPreservedGroups = 2;

    /// <summary>Maximum number of earlier requests and tool calls listed in the summary.</summary>
    public const int MaxSummaryItems = 12;

    /// <summary>Initializes the strategy for a context window and output reservation.</summary>
    public ProcurementCompactionStrategy(int maxContextWindowTokens, int maxOutputTokens)
        : this(Math.Max(1, maxContextWindowTokens - maxOutputTokens))
    {
    }

    /// <summary>Initializes the strategy with an explicit input token budget.</summary>
    public ProcurementCompactionStrategy(int inputBudgetTokens)
        : base(CompactionTriggers.TokensExceed(inputBudgetTokens), CompactionTriggers.TokensBelow(inputBudgetTokens))
    {
        InputBudgetTokens = inputBudgetTokens;
    }

    /// <summary>Gets the token budget that triggers compaction.</summary>
    public int InputBudgetTokens { get; }

    /// <summary>Gets how many times compaction has fired.</summary>
    public int CompactionCount { get; private set; }

    /// <summary>Gets the included token count (as measured by the compaction index) after the last compaction.</summary>
    public int LastIncludedTokenCount { get; private set; }

    /// <inheritdoc />
    protected override ValueTask<bool> CompactCoreAsync(CompactionMessageIndex index, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        List<CompactionMessageGroup> candidates = index.Groups
            .Where(g => !g.IsExcluded && !ProtectedFactDetector.IsProtected(g))
            .SkipLast(MinimumPreservedGroups)
            .ToList();

        var rfpIds = new SortedSet<string>(index.Groups.SelectMany(ProtectedFactDetector.RfpIds), StringComparer.Ordinal);
        var digest = new List<string>();
        var excludedUserRequests = new List<string>();
        int excluded = 0;

        // Leave headroom for the summary group that is inserted afterwards.
        int target = InputBudgetTokens - Math.Max(256, InputBudgetTokens / 10);
        var queue = new Queue<CompactionMessageGroup>(candidates);
        while (queue.Count > 0 && index.IncludedTokenCount > target)
        {
            Exclude(queue.Dequeue(), ref excluded, digest, excludedUserRequests);
        }

        if (excluded == 0)
        {
            return ValueTask.FromResult(false);
        }

        int insertAt = index.Groups.TakeWhile(g => g.Kind == CompactionGroupKind.System).Count();
        var summary = new ChatMessage(ChatRole.User, BuildSummary(excluded, rfpIds, excludedUserRequests, digest));
        summary.AdditionalProperties ??= [];
        summary.AdditionalProperties[CompactionMessageGroup.SummaryPropertyKey] = true;
        index.InsertGroup(insertAt, CompactionGroupKind.Summary, [summary]);

        // The summary's own tokens count too: keep excluding if the budget is still exceeded.
        while (queue.Count > 0 && index.IncludedTokenCount > InputBudgetTokens)
        {
            Exclude(queue.Dequeue(), ref excluded, digest, excludedUserRequests);
        }

        CompactionCount++;
        LastIncludedTokenCount = index.IncludedTokenCount;
        logger?.LogInformation("Procurement compaction excluded {Excluded} groups; {Tokens} tokens now included (budget {Budget}).", excluded, index.IncludedTokenCount, InputBudgetTokens);
        return ValueTask.FromResult(true);
    }

    private static void Exclude(CompactionMessageGroup group, ref int excluded, List<string> digest, List<string> requests)
    {
        group.IsExcluded = true;
        group.ExcludeReason = "ProcurementCompactionStrategy";
        excluded++;
        digest.AddRange(ProtectedFactDetector.ToolCallDigest(group));
        if (group.Kind == CompactionGroupKind.User)
        {
            requests.Add(Trim(group.Messages[0].Text));
        }
    }

    private static string BuildSummary(int excluded, IEnumerable<string> rfpIds, List<string> requests, List<string> digest)
    {
        var sb = new StringBuilder();
        sb.Append("[Conversation summary — ").Append(excluded).AppendLine(" earlier message groups were compacted. Scores, approvals and clarifications recorded so far are kept verbatim below this summary.]");
        string ids = string.Join(", ", rfpIds);
        if (ids.Length > 0)
        {
            sb.Append("Active RFP(s): ").AppendLine(ids);
        }

        if (requests.Count > 0)
        {
            sb.Append("Earlier analyst requests: ").AppendLine(string.Join(" | ", requests.Take(2).Concat(requests.Skip(2).TakeLast(MaxSummaryItems - 2))));
        }

        if (digest.Count > 0)
        {
            sb.Append("Earlier tool calls: ").AppendLine(string.Join("; ", digest.TakeLast(MaxSummaryItems)));
        }

        return sb.ToString().TrimEnd();
    }

    private static string Trim(string? text)
    {
        string t = (text ?? string.Empty).Replace('\n', ' ').Trim();
        return t.Length <= 80 ? t : t[..80] + "…";
    }
}
