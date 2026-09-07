using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ProcurementCopilot.Agent.Looping;

/// <summary>Builds the optional AI judge evaluator. Sends the request and the latest response to a second model call.</summary>
public static class JudgeEvaluatorFactory
{
    /// <summary>Criteria the judge checks the award memo against.</summary>
    public static readonly IReadOnlyList<string> Criteria =
    [
        "Every bid of the RFP has a weighted score cited from the score_bid tool.",
        "Every vendor has a compliance verdict; sanctioned vendors are excluded from the recommendation.",
        "An award memo following the award-memo skill template (summary, scoring table, compliance, risks, recommendation, next steps) has been written to the workspace output folder.",
        "The recommendation names one vendor with a rationale that cites tool results.",
    ];

    /// <summary>Creates the judge evaluator over the judge chat client.</summary>
    public static AIJudgeLoopEvaluator Create(IChatClient judgeClient) =>
        new(judgeClient, new AIJudgeLoopEvaluatorOptions { Criteria = Criteria });
}
