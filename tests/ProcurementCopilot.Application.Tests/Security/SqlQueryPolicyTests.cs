using ProcurementCopilot.Application.Security;
using Shouldly;

namespace ProcurementCopilot.Application.Tests.Security;

public class SqlQueryPolicyTests
{
    private readonly SqlQueryPolicy _sut = new();

    [Theory]
    [InlineData("SELECT TOP 10 VendorName, CreditRating FROM copilot.Vendors ORDER BY CreditRating DESC")]
    [InlineData("select * from copilot.Bids where RfpId = 'RFP-2026-017'")]
    [InlineData("SELECT b.BidId, v.VendorName FROM copilot.Bids b JOIN copilot.Vendors v ON v.BusinessEntityID = b.BusinessEntityID")]
    [InlineData("SELECT VendorId, COUNT(*) AS Orders FROM copilot.PurchaseOrders GROUP BY VendorId HAVING COUNT(*) > 5")]
    [InlineData("WITH q AS (SELECT VendorId, StandardPrice FROM copilot.ProductVendorQuotes WHERE ProductID = 930) SELECT * FROM q")]
    [InlineData("SELECT * FROM [copilot].[Products] WHERE ProductName LIKE '%Tire%'")]
    [InlineData("SELECT Notes FROM copilot.Vendors WHERE Notes LIKE '%select from vendor%'")]
    [InlineData("SELECT x.VendorName FROM (SELECT VendorName FROM copilot.Vendors) AS x")]
    [InlineData("SELECT p.ProductName, q.StandardPrice FROM copilot.Products p CROSS APPLY (SELECT TOP 1 StandardPrice FROM copilot.ProductVendorQuotes q WHERE q.ProductID = p.ProductID) q")]
    public void Evaluate_AllowedSelect_IsAllowed(string sql)
    {
        SqlQueryVerdict verdict = _sut.Evaluate(sql);

        verdict.Code.ShouldBe(SqlQueryPolicyCode.Allowed, verdict.Reason);
        verdict.Sql.ShouldBe(sql.Trim());
    }

    [Theory]
    [InlineData("", SqlQueryPolicyCode.EmptyQuery)]
    [InlineData("   ", SqlQueryPolicyCode.EmptyQuery)]
    [InlineData("SELECT * FROM copilot.Vendors; DROP TABLE Procurement.Bid", SqlQueryPolicyCode.MultipleStatementsNotAllowed)]
    [InlineData("SELECT * FROM copilot.Vendors\nGO\nSELECT 1", SqlQueryPolicyCode.MultipleStatementsNotAllowed)]
    [InlineData("SELECT * FROM copilot.Vendors -- WHERE 1=1", SqlQueryPolicyCode.CommentsNotAllowed)]
    [InlineData("SELECT /* hidden */ * FROM copilot.Vendors", SqlQueryPolicyCode.CommentsNotAllowed)]
    [InlineData("DELETE FROM copilot.Bids", SqlQueryPolicyCode.MustBeSelect)]
    [InlineData("EXEC copilot.usp_RecordAwardRecommendation 'RFP-2026-017','VND-1678','x',1,1,1", SqlQueryPolicyCode.MustBeSelect)]
    [InlineData("INSERT INTO Procurement.Bid SELECT * FROM copilot.Bids", SqlQueryPolicyCode.MustBeSelect)]
    [InlineData("SELECT * INTO #t FROM copilot.Vendors", SqlQueryPolicyCode.ForbiddenKeyword)]
    [InlineData("SELECT * FROM copilot.Vendors WHERE 1 = 1 UNION SELECT name, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 FROM sys.tables", SqlQueryPolicyCode.ForbiddenKeyword)]
    [InlineData("SELECT * FROM copilot.Vendors WAITFOR DELAY '00:00:10'", SqlQueryPolicyCode.ForbiddenKeyword)]
    [InlineData("SELECT * FROM OPENROWSET('SQLNCLI', 'Server=x', 'SELECT 1')", SqlQueryPolicyCode.ForbiddenKeyword)]
    [InlineData("SELECT * FROM copilot.Vendors WHERE VendorName = @name", SqlQueryPolicyCode.VariablesNotAllowed)]
    [InlineData("SELECT @@VERSION", SqlQueryPolicyCode.VariablesNotAllowed)]
    [InlineData("SELECT * FROM AdventureWorks2019.Purchasing.Vendor", SqlQueryPolicyCode.CrossDatabaseNotAllowed)]
    [InlineData("SELECT * FROM copilot.Vendors v JOIN master.dbo.sysdatabases d ON 1 = 1", SqlQueryPolicyCode.ForbiddenKeyword)]
    [InlineData("SELECT * FROM Purchasing.Vendor", SqlQueryPolicyCode.SchemaNotAllowed)]
    [InlineData("SELECT * FROM Procurement.Bid", SqlQueryPolicyCode.SchemaNotAllowed)]
    [InlineData("SELECT * FROM copilot.Vendors v JOIN Purchasing.ProductVendor pv ON pv.BusinessEntityID = v.BusinessEntityID", SqlQueryPolicyCode.SchemaNotAllowed)]
    [InlineData("SELECT * FROM Vendors", SqlQueryPolicyCode.SchemaNotAllowed)]
    [InlineData("SELECT * FROM dbo.Vendors", SqlQueryPolicyCode.SchemaNotAllowed)]
    [InlineData("SELECT * FROM INFORMATION_SCHEMA.TABLES", SqlQueryPolicyCode.ForbiddenKeyword)]
    [InlineData("SELECT * FROM copilot.Vendors REVERT", SqlQueryPolicyCode.ForbiddenKeyword)]
    public void Evaluate_HostileQuery_IsDeniedWithSpecificCode(string sql, SqlQueryPolicyCode expected)
    {
        SqlQueryVerdict verdict = _sut.Evaluate(sql);

        verdict.IsAllowed.ShouldBeFalse();
        verdict.Code.ShouldBe(expected);
        verdict.Sql.ShouldBeEmpty();
    }

    [Fact]
    public void Evaluate_TooLong_IsRejected()
    {
        _sut.Evaluate("SELECT '" + new string('a', SqlQueryPolicy.MaxLength) + "' FROM copilot.Vendors").Code.ShouldBe(SqlQueryPolicyCode.TooLong);
    }

    [Fact]
    public void Evaluate_ForbiddenKeyword_NamesTheKeyword()
    {
        _sut.Evaluate("SELECT * FROM copilot.Vendors WHERE 1 = 1 AND EXISTS (SELECT 1 FROM copilot.Bids) EXEC xp_cmdshell 'dir'").Reason.ShouldContain("EXEC");
    }
}
