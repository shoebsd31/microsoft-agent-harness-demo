using ProcurementCopilot.Application.Configuration;
using ProcurementCopilot.Application.Security;
using ProcurementCopilot.Domain.Common;
using Shouldly;

namespace ProcurementCopilot.Application.Tests.Security;

public class WorkspacePathPolicyTests
{
    private readonly WorkspacePathPolicy _sut = TestPaths.Policy();

    [Theory]
    [InlineData("../data/vendors.json", "Path.Traversal")]
    [InlineData("rfps\\..\\..\\x", "Path.Traversal")]
    [InlineData("rfps/../../x.md", "Path.Traversal")]
    [InlineData("/etc/passwd", "Path.Absolute")]
    [InlineData("C:\\Windows\\System32\\drivers\\etc\\hosts", "Path.Absolute")]
    [InlineData("\\\\server\\share\\file.md", "Path.Unc")]
    [InlineData("//server/share/file.md", "Path.Unc")]
    [InlineData("", "Path.Empty")]
    [InlineData("rfps/a\0b.md", "Path.Invalid")]
    public void Resolve_HostileInput_IsRejected(string input, string code)
    {
        Result<string> result = _sut.Resolve(input, PathAccess.Read);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(code);
    }

    [Fact]
    public void Resolve_ReadInsideRfps_Succeeds()
    {
        Result<string> result = _sut.Resolve("rfps/RFP-2026-017/rfp.md", PathAccess.Read);

        result.IsSuccess.ShouldBeTrue();
        WorkspacePathPolicy.IsUnder(result.Value, _sut.Root).ShouldBeTrue();
    }

    [Fact]
    public void Resolve_WriteInsideRfps_IsReadOnly()
    {
        _sut.Resolve("rfps/RFP-2026-017/new.md", PathAccess.Write).Error.Code.ShouldBe("Path.ReadOnly");
    }

    [Fact]
    public void Resolve_WriteInsideOutput_Succeeds()
    {
        _sut.Resolve("output/award-memo.md", PathAccess.Write).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_WriteDisallowedExtension_IsRejected()
    {
        _sut.Resolve("output/payload.exe", PathAccess.Write).Error.Code.ShouldBe("Path.Extension");
    }

    [Fact]
    public void Resolve_ReadOutsideSharedFolders_IsRejected()
    {
        _sut.Resolve("secrets/key.txt", PathAccess.Read).Error.Code.ShouldBe("Path.NotShared");
    }

    [Fact]
    public void Resolve_RootPrefixTrick_IsRejected()
    {
        // A sibling directory whose name starts with the root name must not pass the "under root" check.
        string root = TestPaths.NewWorkspace();
        Directory.CreateDirectory(root + "-evil");
        var sut = new WorkspacePathPolicy(root, new WorkspaceOptions());

        WorkspacePathPolicy.IsUnder(root + "-evil", root).ShouldBeFalse();
        sut.Resolve("../" + Path.GetFileName(root) + "-evil/x.md", PathAccess.Read).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void CheckSize_AboveLimit_IsRejected()
    {
        _sut.CheckSize(1_048_577).Error.Code.ShouldBe("Path.TooLarge");
        _sut.CheckSize(10).IsSuccess.ShouldBeTrue();
    }
}
