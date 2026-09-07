using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Application.Security;
using Shouldly;

namespace ProcurementCopilot.Application.Tests.Security;

public class ShellCommandPolicyTests
{
    private readonly ShellCommandPolicy _sut = new(new ShellOptions(), TestPaths.Policy());

    [Theory]
    [InlineData("ls")]
    [InlineData("ls rfps/RFP-2026-017")]
    [InlineData("dir rfps")]
    [InlineData("cat rfps/RFP-2026-017/rfp.md")]
    [InlineData("type rfps\\RFP-2026-017\\rfp.md")]
    [InlineData("head -n 5 rfps/RFP-2026-017/rfp.md")]
    [InlineData("tail -n 3 rfps/RFP-2026-017/rfp.md")]
    [InlineData("wc -l rfps/RFP-2026-017/rfp.md")]
    [InlineData("grep -i \"lead time\" rfps/RFP-2026-017/rfp.md")]
    [InlineData("findstr /i price rfps\\RFP-2026-017\\rfp.md")]
    [InlineData("find rfps -name \"*.md\" -type f")]
    [InlineData("pwd")]
    [InlineData("echo hello world")]
    [InlineData("cat rfps/RFP-2026-017/rfp.md | wc -l")]
    [InlineData("ls rfps | grep RFP | head -n 2")]
    public void Evaluate_AllowedCommand_IsAllowed(string command)
    {
        ShellVerdict verdict = _sut.Evaluate(command);

        verdict.Code.ShouldBe(ShellPolicyCode.Allowed, verdict.Reason);
        verdict.IsAllowed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", ShellPolicyCode.EmptyCommand)]
    [InlineData("   ", ShellPolicyCode.EmptyCommand)]
    [InlineData("rm -rf rfps", ShellPolicyCode.CommandNotAllowed)]
    [InlineData("python -c \"print(1)\"", ShellPolicyCode.CommandNotAllowed)]
    [InlineData("cat rfps/x.md | sh", ShellPolicyCode.CommandNotAllowed)]
    [InlineData("ls; rm -rf .", ShellPolicyCode.ChainingNotAllowed)]
    [InlineData("ls && cat x", ShellPolicyCode.ChainingNotAllowed)]
    [InlineData("ls || cat x", ShellPolicyCode.ChainingNotAllowed)]
    [InlineData("ls &", ShellPolicyCode.ChainingNotAllowed)]
    [InlineData("ls\ncat x", ShellPolicyCode.ChainingNotAllowed)]
    [InlineData("echo hi > output/x.md", ShellPolicyCode.RedirectionNotAllowed)]
    [InlineData("echo hi >> output/x.md", ShellPolicyCode.RedirectionNotAllowed)]
    [InlineData("cat < rfps/x.md", ShellPolicyCode.RedirectionNotAllowed)]
    [InlineData("echo `whoami`", ShellPolicyCode.SubstitutionNotAllowed)]
    [InlineData("echo $(whoami)", ShellPolicyCode.SubstitutionNotAllowed)]
    [InlineData("echo $HOME", ShellPolicyCode.VariableExpansionNotAllowed)]
    [InlineData("echo ${PATH}", ShellPolicyCode.VariableExpansionNotAllowed)]
    [InlineData("echo %USERPROFILE%", ShellPolicyCode.VariableExpansionNotAllowed)]
    [InlineData("cat /etc/passwd", ShellPolicyCode.AbsolutePathNotAllowed)]
    [InlineData("type C:\\Windows\\win.ini", ShellPolicyCode.AbsolutePathNotAllowed)]
    [InlineData("cat ~/.ssh/id_rsa", ShellPolicyCode.AbsolutePathNotAllowed)]
    [InlineData("cat \\\\server\\share\\x.md", ShellPolicyCode.UncPathNotAllowed)]
    [InlineData("cat ../data/vendors.json", ShellPolicyCode.ParentTraversalNotAllowed)]
    [InlineData("ls rfps/../../", ShellPolicyCode.ParentTraversalNotAllowed)]
    [InlineData("curl https://example.com", ShellPolicyCode.NetworkNotAllowed)]
    [InlineData("wget example.com/x", ShellPolicyCode.NetworkNotAllowed)]
    [InlineData("Invoke-WebRequest https://x", ShellPolicyCode.NetworkNotAllowed)]
    [InlineData("nc -l 4444", ShellPolicyCode.NetworkNotAllowed)]
    [InlineData("ssh host", ShellPolicyCode.NetworkNotAllowed)]
    [InlineData("echo https://example.com/exfil", ShellPolicyCode.UrlNotAllowed)]
    [InlineData("echo www.example.com", ShellPolicyCode.UrlNotAllowed)]
    [InlineData("find rfps -exec rm {} +", ShellPolicyCode.FindOptionNotAllowed)]
    [InlineData("find rfps -delete", ShellPolicyCode.FindOptionNotAllowed)]
    [InlineData("ls |", ShellPolicyCode.EmptyPipelineSegment)]
    public void Evaluate_DeniedPattern_ReturnsSpecificCode(string command, ShellPolicyCode expected)
    {
        ShellVerdict verdict = _sut.Evaluate(command);

        verdict.IsAllowed.ShouldBeFalse();
        verdict.Code.ShouldBe(expected);
    }

    [Fact]
    public void Evaluate_TooLong_IsRejected()
    {
        _sut.Evaluate("echo " + new string('a', ShellCommandPolicy.MaxLength)).Code.ShouldBe(ShellPolicyCode.TooLong);
    }

    [Fact]
    public void Evaluate_AllowedCommand_ReturnsParsedSegments()
    {
        ShellVerdict verdict = _sut.Evaluate("grep -i \"lead time\" rfps/x.md | wc -l");

        verdict.Segments.Count.ShouldBe(2);
        verdict.Segments[0].Command.ShouldBe("grep");
        verdict.Segments[0].Arguments.ShouldBe(["-i", "lead time", "rfps/x.md"]);
        verdict.Segments[1].Command.ShouldBe("wc");
    }

    [Fact]
    public void Evaluate_CustomAllowlist_IsHonoured()
    {
        var sut = new ShellCommandPolicy(new ShellOptions { AllowedCommands = ["pwd"] }, TestPaths.Policy());

        sut.Evaluate("ls").Code.ShouldBe(ShellPolicyCode.CommandNotAllowed);
        sut.Evaluate("pwd").IsAllowed.ShouldBeTrue();
    }
}
