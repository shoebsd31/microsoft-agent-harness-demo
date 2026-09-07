using ProcurementCopilot.Domain.Compliance;
using ProcurementCopilot.Domain.Currency;
using ProcurementCopilot.Domain.Entities;
using ProcurementCopilot.Domain.ValueObjects;
using Shouldly;

namespace ProcurementCopilot.Domain.Tests;

public class CurrencyConverterTests
{
    private static readonly CurrencyConverter Sut = new(SeedFixtures.Rates());

    [Fact]
    public void Convert_UsdToEur_UsesSeededRateAndRoundsToTwoDecimals()
    {
        Money result = Sut.Convert(Money.Create(199000m, CurrencyCode.Usd).Value, CurrencyCode.Eur).Value;

        result.Amount.ShouldBe(183080.00m);
        result.Currency.ShouldBe(CurrencyCode.Eur);
    }

    [Fact]
    public void Convert_SameCurrency_ReturnsSameAmount()
    {
        Sut.Convert(Money.Create(10m, CurrencyCode.Eur).Value, CurrencyCode.Eur).Value.Amount.ShouldBe(10m);
    }

    [Fact]
    public void FindRate_ReversePairOnlySeeded_UsesInverse()
    {
        FxRate rate = Sut.FindRate(CurrencyCode.Eur, CurrencyCode.Usd).Value;

        rate.Rate.ShouldBe(decimal.Round(1m / 0.92m, 6));
    }

    [Fact]
    public void FindRate_UnknownPair_Fails()
    {
        Sut.FindRate(CurrencyCode.Create("GBP").Value, CurrencyCode.Create("JPY").Value).Error.Code.ShouldBe("Fx.RateNotFound");
    }
}

public class ComplianceCheckerTests
{
    [Fact]
    public void Check_SanctionedVendor_IsBlocked()
    {
        ComplianceResult result = ComplianceChecker.Check(SeedFixtures.Vendors()[VendorId.Create("VND-0004").Value], SeedFixtures.Rfp017(), SeedFixtures.Sanctions());

        result.IsSanctioned.ShouldBeTrue();
        result.Verdict.ShouldStartWith("BLOCKED");
    }

    [Fact]
    public void Check_VendorWithAllCertifications_Passes()
    {
        ComplianceResult result = ComplianceChecker.Check(SeedFixtures.Vendors()[VendorId.Create("VND-0001").Value], SeedFixtures.Rfp017(), SeedFixtures.Sanctions());

        result.HasIssues.ShouldBeFalse();
        result.Verdict.ShouldBe("PASS");
    }

    [Fact]
    public void Check_MissingRequiredCertification_ReportsGap()
    {
        Vendor vendor = SeedFixtures.Vendor("VND-0009", "NoCe", "NL", ["ISO 9001"], 5);

        ComplianceResult result = ComplianceChecker.Check(vendor, SeedFixtures.Rfp017(), []);

        result.MissingCertifications.ShouldBe(["CE"]);
        result.Verdict.ShouldBe("GAP: missing CE");
    }

    [Fact]
    public void Check_RfpWithoutRequiredList_UsesCategoryDefaults()
    {
        Rfp rfp = SeedFixtures.Rfp017() with { RequiredCertifications = [], Category = "Electronics" };
        Vendor vendor = SeedFixtures.Vendor("VND-0009", "NoRohs", "NL", ["ISO 9001", "CE"], 5);

        ComplianceChecker.Check(vendor, rfp, []).MissingCertifications.ShouldBe(["RoHS"]);
    }
}
