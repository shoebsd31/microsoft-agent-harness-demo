using ProcurementCopilot.Domain.Common;
using ProcurementCopilot.Domain.ValueObjects;
using Shouldly;

namespace ProcurementCopilot.Domain.Tests;

public class ValueObjectTests
{
    [Theory]
    [InlineData("RFP-2026-017", true)]
    [InlineData("rfp-2026-017", false)]
    [InlineData("RFP-26-017", false)]
    [InlineData("RFP-2026-0170", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("RFP-2026-017; DROP TABLE", false)]
    public void RfpId_Create_ValidatesStrictFormat(string? input, bool valid)
    {
        RfpId.Create(input).IsSuccess.ShouldBe(valid);
    }

    [Theory]
    [InlineData("VND-0001", true)]
    [InlineData("VND-001", false)]
    [InlineData("VND-00011", false)]
    [InlineData("vnd-0001", false)]
    [InlineData("../VND-0001", false)]
    public void VendorId_Create_ValidatesStrictFormat(string input, bool valid)
    {
        VendorId.Create(input).IsSuccess.ShouldBe(valid);
    }

    [Theory]
    [InlineData("BID-001", true)]
    [InlineData("BID-0001", false)]
    [InlineData("BID-1", false)]
    public void BidId_Create_ValidatesStrictFormat(string input, bool valid)
    {
        BidId.Create(input).IsSuccess.ShouldBe(valid);
    }

    [Fact]
    public void RfpId_Create_InvalidInput_ReturnsCatalogError()
    {
        RfpId.Create("nope").Error.Code.ShouldBe("RfpId.InvalidFormat");
    }

    [Theory]
    [InlineData("EUR", true)]
    [InlineData(" usd ", true)]
    [InlineData("XXX", false)]
    [InlineData("BTC", false)]
    [InlineData("", false)]
    public void CurrencyCode_Create_UsesAllowlist(string input, bool valid)
    {
        CurrencyCode.Create(input).IsSuccess.ShouldBe(valid);
    }

    [Fact]
    public void CurrencyCode_Create_NormalisesCase()
    {
        CurrencyCode.Create("usd").Value.ShouldBe(CurrencyCode.Usd);
    }

    [Fact]
    public void Money_Create_NegativeAmount_Fails()
    {
        Money.Create(-1m, CurrencyCode.Eur).Error.Code.ShouldBe("Money.InvalidAmount");
    }

    [Fact]
    public void Money_Add_DifferentCurrencies_Fails()
    {
        Money eur = Money.Create(1m, CurrencyCode.Eur).Value;
        Money usd = Money.Create(1m, CurrencyCode.Usd).Value;

        eur.Add(usd).Error.Code.ShouldBe("Money.CurrencyMismatch");
    }

    [Fact]
    public void Money_ToString_UsesTwoDecimalsAndCode()
    {
        Money.Create(1234.5m, CurrencyCode.Eur).Value.ToString().ShouldBe("1,234.50 EUR");
    }

    [Fact]
    public void Result_Value_OnFailure_Throws()
    {
        Result<int> failed = Error.Validation("x", "y");

        Should.Throw<InvalidOperationException>(() => failed.Value);
        failed.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Result_MapAndBind_PropagateFailures()
    {
        Result<int> failed = Error.Validation("x", "y");

        failed.Map(v => v + 1).IsFailure.ShouldBeTrue();
        failed.Bind(v => Result<string>.Success("ok")).Error.Code.ShouldBe("x");
        Result<int>.Success(2).Map(v => v * 2).Value.ShouldBe(4);
    }

    [Fact]
    public void Result_Match_SelectsBranch()
    {
        Result<int>.Success(1).Match(v => "s", e => "f").ShouldBe("s");
        Result<int>.Failure(Error.None with { Code = "c" }).Match(v => "s", e => "f").ShouldBe("f");
    }
}
