-- 0002_copilot_views.sql
-- The `copilot` schema is the ONLY surface the agent's tools and the approval-gated query_readonly tool can see.
-- Views join the Procurement tables with AdventureWorks data and expose stable, model-friendly column names.
-- Vendor ids are exposed as 'VND-' + 4-digit BusinessEntityID, bid ids as 'BID-nnn'.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF SCHEMA_ID('copilot') IS NULL
    EXEC('CREATE SCHEMA copilot AUTHORIZATION dbo');
GO

CREATE OR ALTER VIEW copilot.Vendors AS
SELECT
    v.BusinessEntityID,
    'VND-' + RIGHT('0000' + CONVERT(varchar(4), v.BusinessEntityID), 4)  AS VendorId,
    v.Name                                                              AS VendorName,
    v.AccountNumber,
    v.CreditRating,
    v.PreferredVendorStatus,
    v.ActiveFlag,
    COALESCE(vp.CountryOverride, addr.Country, 'Unknown')               AS Country,
    vp.YearsTrading,
    vp.Notes,
    (SELECT STRING_AGG(vc.Certification, ', ') WITHIN GROUP (ORDER BY vc.Certification)
       FROM Procurement.VendorCertification vc WHERE vc.BusinessEntityID = v.BusinessEntityID) AS Certifications,
    CASE WHEN rp.BusinessEntityID IS NULL THEN 0 ELSE 1 END             AS IsRestricted,
    (SELECT TOP 1 e.EmailAddress
       FROM Person.BusinessEntityContact bec
       JOIN Person.EmailAddress e ON e.BusinessEntityID = bec.PersonID
      WHERE bec.BusinessEntityID = v.BusinessEntityID
      ORDER BY e.EmailAddressID)                                        AS ContactEmail
FROM Purchasing.Vendor v
LEFT JOIN Procurement.VendorProfile vp ON vp.BusinessEntityID = v.BusinessEntityID
LEFT JOIN Procurement.RestrictedParty rp ON rp.BusinessEntityID = v.BusinessEntityID
OUTER APPLY (
    SELECT TOP 1 cr.Name AS Country
      FROM Person.BusinessEntityAddress bea
      JOIN Person.Address a ON a.AddressID = bea.AddressID
      JOIN Person.StateProvince sp ON sp.StateProvinceID = a.StateProvinceID
      JOIN Person.CountryRegion cr ON cr.CountryRegionCode = sp.CountryRegionCode
     WHERE bea.BusinessEntityID = v.BusinessEntityID
     ORDER BY bea.AddressTypeID) addr;
GO

CREATE OR ALTER VIEW copilot.VendorCertifications AS
SELECT 'VND-' + RIGHT('0000' + CONVERT(varchar(4), vc.BusinessEntityID), 4) AS VendorId, vc.BusinessEntityID, vc.Certification
FROM Procurement.VendorCertification vc;
GO

CREATE OR ALTER VIEW copilot.Rfps AS
SELECT
    r.RfpId, r.Title, r.Description, r.ProductID,
    p.Name AS ProductName, p.ProductNumber,
    r.Quantity, r.CurrencyCode, r.Status, r.Category,
    r.WeightPrice, r.WeightLeadTime, r.WeightWarranty, r.WeightTechnical, r.WeightSustainability,
    (SELECT STRING_AGG(rc.Certification, ', ') WITHIN GROUP (ORDER BY rc.Certification)
       FROM Procurement.RfpRequiredCertification rc WHERE rc.RfpId = r.RfpId) AS RequiredCertifications,
    (SELECT COUNT(*) FROM Procurement.Bid b WHERE b.RfpId = r.RfpId)         AS BidCount,
    r.CreatedAt, r.ModifiedAt
FROM Procurement.Rfp r
JOIN Production.Product p ON p.ProductID = r.ProductID;
GO

CREATE OR ALTER VIEW copilot.Bids AS
SELECT
    b.BidId, b.BidNumber, b.RfpId,
    'VND-' + RIGHT('0000' + CONVERT(varchar(4), b.BusinessEntityID), 4) AS VendorId,
    b.BusinessEntityID, v.Name AS VendorName,
    b.UnitPrice, b.CurrencyCode, b.LeadTimeWeeks, b.WarrantyMonths, b.TechnicalCompliancePercent,
    b.DeliveryClause, b.Notes, b.SubmittedAt, b.ModifiedAt
FROM Procurement.Bid b
JOIN Purchasing.Vendor v ON v.BusinessEntityID = b.BusinessEntityID;
GO

CREATE OR ALTER VIEW copilot.RestrictedParties AS
SELECT 'VND-' + RIGHT('0000' + CONVERT(varchar(4), rp.BusinessEntityID), 4) AS VendorId,
       rp.BusinessEntityID, v.Name AS VendorName, rp.ListName, rp.ListedOn, rp.Reason
FROM Procurement.RestrictedParty rp
JOIN Purchasing.Vendor v ON v.BusinessEntityID = rp.BusinessEntityID;
GO

-- Latest end-of-day rate per currency pair from Sales.CurrencyRate (AdventureWorks rates are USD -> X).
CREATE OR ALTER VIEW copilot.CurrencyRates AS
SELECT cr.FromCurrencyCode, cr.ToCurrencyCode, cr.EndOfDayRate AS Rate, CONVERT(date, cr.CurrencyRateDate) AS AsOf
FROM Sales.CurrencyRate cr
JOIN (SELECT FromCurrencyCode, ToCurrencyCode, MAX(CurrencyRateDate) AS LatestDate
        FROM Sales.CurrencyRate GROUP BY FromCurrencyCode, ToCurrencyCode) latest
  ON latest.FromCurrencyCode = cr.FromCurrencyCode AND latest.ToCurrencyCode = cr.ToCurrencyCode AND latest.LatestDate = cr.CurrencyRateDate;
GO

CREATE OR ALTER VIEW copilot.Products AS
SELECT p.ProductID, p.Name AS ProductName, p.ProductNumber, p.StandardCost, p.ListPrice, p.Color, p.SafetyStockLevel, p.ReorderPoint,
       psc.Name AS Subcategory, pc.Name AS Category, p.SellStartDate, p.SellEndDate, p.DiscontinuedDate
FROM Production.Product p
LEFT JOIN Production.ProductSubcategory psc ON psc.ProductSubcategoryID = p.ProductSubcategoryID
LEFT JOIN Production.ProductCategory pc ON pc.ProductCategoryID = psc.ProductCategoryID;
GO

-- Standing quotes from AdventureWorks (Purchasing.ProductVendor): market context for the agents.
CREATE OR ALTER VIEW copilot.ProductVendorQuotes AS
SELECT pv.ProductID, p.Name AS ProductName, p.ProductNumber,
       'VND-' + RIGHT('0000' + CONVERT(varchar(4), pv.BusinessEntityID), 4) AS VendorId, v.Name AS VendorName,
       pv.StandardPrice, pv.LastReceiptCost, pv.LastReceiptDate, pv.AverageLeadTime AS AverageLeadTimeDays,
       pv.MinOrderQty, pv.MaxOrderQty, pv.OnOrderQty, pv.UnitMeasureCode
FROM Purchasing.ProductVendor pv
JOIN Production.Product p ON p.ProductID = pv.ProductID
JOIN Purchasing.Vendor v ON v.BusinessEntityID = pv.BusinessEntityID;
GO

CREATE OR ALTER VIEW copilot.PurchaseOrders AS
SELECT h.PurchaseOrderID, h.RevisionNumber, h.Status,
       CASE h.Status WHEN 1 THEN 'Pending' WHEN 2 THEN 'Approved' WHEN 3 THEN 'Rejected' WHEN 4 THEN 'Complete' END AS StatusName,
       'VND-' + RIGHT('0000' + CONVERT(varchar(4), h.VendorID), 4) AS VendorId, v.Name AS VendorName,
       h.EmployeeID, sm.Name AS ShipMethod, h.OrderDate, h.ShipDate, h.SubTotal, h.TaxAmt, h.Freight, h.TotalDue,
       (SELECT COUNT(*) FROM Purchasing.PurchaseOrderDetail d WHERE d.PurchaseOrderID = h.PurchaseOrderID) AS LineCount
FROM Purchasing.PurchaseOrderHeader h
JOIN Purchasing.Vendor v ON v.BusinessEntityID = h.VendorID
JOIN Purchasing.ShipMethod sm ON sm.ShipMethodID = h.ShipMethodID;
GO

CREATE OR ALTER VIEW copilot.PurchaseOrderLines AS
SELECT d.PurchaseOrderID, d.PurchaseOrderDetailID, d.ProductID, p.Name AS ProductName, d.DueDate, d.OrderQty, d.UnitPrice, d.LineTotal,
       d.ReceivedQty, d.RejectedQty, d.StockedQty
FROM Purchasing.PurchaseOrderDetail d
JOIN Production.Product p ON p.ProductID = d.ProductID;
GO

CREATE OR ALTER VIEW copilot.AwardRecommendations AS
SELECT ar.AwardRecommendationID, ar.RfpId,
       'VND-' + RIGHT('0000' + CONVERT(varchar(4), ar.BusinessEntityID), 4) AS VendorId, v.Name AS VendorName,
       ar.PurchaseOrderID, ar.WinningScore, ar.Rationale, ar.RecordedAt, ar.RecordedBy
FROM Procurement.AwardRecommendation ar
JOIN Purchasing.Vendor v ON v.BusinessEntityID = ar.BusinessEntityID;
GO

IF NOT EXISTS (SELECT 1 FROM Procurement.SchemaMigration WHERE ScriptName = '0002_copilot_views.sql')
    INSERT INTO Procurement.SchemaMigration (ScriptName) VALUES ('0002_copilot_views.sql');
GO
