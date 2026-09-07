using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Common;
using Shouldly;

namespace ProcurementCopilot.Application.Tests.Security;

public class ApprovalPolicyTests
{
    private readonly ApprovalPolicy _sut = new(new ApprovalPolicyOptions());

    [Theory]
    [InlineData("draft_clarification_email", true)]
    [InlineData("record_award_recommendation", true)]
    [InlineData("shell", true)]
    [InlineData("SHELL", true)]
    [InlineData("score_bid", false)]
    [InlineData("list_open_rfps", false)]
    public void RequiresApproval_UsesConfiguredList(string tool, bool expected)
    {
        _sut.RequiresApproval(tool).ShouldBe(expected);
    }

    [Fact]
    public async Task GuardRules_ListedTool_IsNeverAutoApprovedEvenWhenRuleMatches()
    {
        Func<string, string, ValueTask<bool>> guarded = _sut.GuardRules<string>([_ => ValueTask.FromResult(true)]);

        (await guarded("draft_clarification_email", "ctx")).ShouldBeFalse();
        (await guarded("shell", "ctx")).ShouldBeFalse();
    }

    [Fact]
    public async Task GuardRules_UnlistedTool_DelegatesToRulesInOrder()
    {
        var calls = new List<string>();
        Func<string, string, ValueTask<bool>> guarded = _sut.GuardRules<string>(
        [
            ctx => { calls.Add("first"); return ValueTask.FromResult(false); },
            ctx => { calls.Add("second"); return ValueTask.FromResult(true); },
            ctx => { calls.Add("third"); return ValueTask.FromResult(true); },
        ]);

        (await guarded("score_bid", "ctx")).ShouldBeTrue();
        calls.ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task GuardRules_NoRuleMatches_ReturnsFalse()
    {
        Func<string, string, ValueTask<bool>> guarded = _sut.GuardRules<string>([_ => ValueTask.FromResult(false)]);

        (await guarded("score_bid", "ctx")).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_ConfigIsSingleSourceOfTruth()
    {
        var custom = new ApprovalPolicy(new ApprovalPolicyOptions { RequireApprovalFor = ["score_bid"] });

        custom.RequiresApproval("score_bid").ShouldBeTrue();
        custom.RequiresApproval("shell").ShouldBeFalse();
    }
}

public class ModeGuardTests
{
    [Fact]
    public void Check_PlanMode_BlocksWithInstruction()
    {
        Result<string> result = ModeGuard.Check(AgentModes.Plan, "draft_clarification_email");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ModeGuard.BlockedCode);
        result.Error.Message.ShouldContain("/mode execute");
    }

    [Fact]
    public void Check_UnknownMode_FailsClosed()
    {
        ModeGuard.Check(null, "x").IsFailure.ShouldBeTrue();
        ModeGuard.Check("review", "x").IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Check_ExecuteMode_Allows()
    {
        ModeGuard.Check("EXECUTE", "x").IsSuccess.ShouldBeTrue();
    }
}

public class ToolArgumentValidatorTests
{
    [Fact]
    public void Text_TooLong_IsRejected()
    {
        ToolArgumentValidator.Text(new string('a', 4001), "body").Error.Code.ShouldBe("body.TooLong");
        ToolArgumentValidator.Text(new string('a', 4000), "body").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Text_Empty_IsRejected()
    {
        ToolArgumentValidator.Text("   ", "subject").Error.Code.ShouldBe("subject.Required");
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1_000_000_001, false)]
    public void Amount_Range_IsEnforced(decimal amount, bool valid)
    {
        ToolArgumentValidator.Amount(amount).IsSuccess.ShouldBe(valid);
    }

    [Fact]
    public void Ids_AreTrimmedAndValidated()
    {
        ToolArgumentValidator.RfpId(" RFP-2026-017 ").IsSuccess.ShouldBeTrue();
        ToolArgumentValidator.VendorId("VND-1").IsFailure.ShouldBeTrue();
        ToolArgumentValidator.BidId("BID-001").IsSuccess.ShouldBeTrue();
        ToolArgumentValidator.Currency("ZZZ").IsFailure.ShouldBeTrue();
    }
}
