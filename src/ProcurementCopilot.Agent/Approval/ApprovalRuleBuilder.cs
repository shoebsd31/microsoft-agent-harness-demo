using Microsoft.Agents.AI;
using ProcurementCopilot.Agent.Tools;
using ProcurementCopilot.Application.Security;

namespace ProcurementCopilot.Agent.Approval;

/// <summary>
/// Builds the harness auto-approval rules: the built-in read-only rules for skills and file access plus a rule for the
/// procurement read tools, all guarded by <see cref="ApprovalPolicy"/> so a listed tool is never auto-approved.
/// </summary>
public static class ApprovalRuleBuilder
{
    /// <summary>Auto-approves the procurement read-only tools by name.</summary>
    public static ValueTask<bool> ReadOnlyProcurementToolsRule(ToolAutoApprovalRuleContext context) =>
        ValueTask.FromResult(ToolNames.ReadOnly.Contains(context.FunctionCallContent.Name));

    /// <summary>Returns the rule list for <see cref="ToolApprovalAgentOptions.AutoApprovalRules"/>.</summary>
    public static IReadOnlyList<Func<ToolAutoApprovalRuleContext, ValueTask<bool>>> Build(ApprovalPolicy policy)
    {
        Func<string, ToolAutoApprovalRuleContext, ValueTask<bool>> guarded = policy.GuardRules<ToolAutoApprovalRuleContext>(
        [
            AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule,
            FileAccessProvider.ReadOnlyToolsAutoApprovalRule,
            ReadOnlyProcurementToolsRule,
        ]);
        return [context => guarded(context.FunctionCallContent.Name, context)];
    }
}
