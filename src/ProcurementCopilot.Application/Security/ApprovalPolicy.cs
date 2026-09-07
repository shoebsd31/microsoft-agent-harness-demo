using ProcurementCopilot.Application.Configuration;

namespace ProcurementCopilot.Application.Security;

/// <summary>
/// The single source of truth for which tools need a human in the loop. Built from
/// <see cref="ApprovalPolicyOptions.RequireApprovalFor"/>; a listed tool can never be auto-approved by any rule.
/// </summary>
public sealed class ApprovalPolicy
{
    private readonly HashSet<string> _requireApproval;

    /// <summary>Initializes the policy from configuration.</summary>
    public ApprovalPolicy(ApprovalPolicyOptions options)
    {
        _requireApproval = new HashSet<string>(options.EffectiveRequireApprovalFor, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets the tool names that require approval.</summary>
    public IReadOnlySet<string> ToolsRequiringApproval => _requireApproval;

    /// <summary>Returns <see langword="true"/> when the tool must be approved by a human before it runs.</summary>
    public bool RequiresApproval(string toolName) => _requireApproval.Contains(toolName);

    /// <summary>
    /// Wraps a set of auto-approval rules so they are skipped entirely for tools that require approval.
    /// The returned rule returns <see langword="false"/> (no auto-approval) for listed tools regardless of the inner rules.
    /// </summary>
    public Func<string, TContext, ValueTask<bool>> GuardRules<TContext>(IEnumerable<Func<TContext, ValueTask<bool>>> innerRules)
    {
        Func<TContext, ValueTask<bool>>[] rules = innerRules.ToArray();
        return async (toolName, context) =>
        {
            if (RequiresApproval(toolName))
            {
                return false;
            }

            foreach (Func<TContext, ValueTask<bool>> rule in rules)
            {
                if (await rule(context).ConfigureAwait(false))
                {
                    return true;
                }
            }

            return false;
        };
    }
}
