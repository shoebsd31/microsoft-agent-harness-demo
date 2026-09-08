-- 0003_copilot_procedures_and_security.sql
-- Write path: the only way the agent changes AdventureWorks is through copilot.usp_RecordAwardRecommendation
-- (called by the approval-gated record_award_recommendation tool). It inserts a PENDING purchase order.
-- Read path for the approval-gated query_readonly tool: the database user copilot_reader (no login) may SELECT
-- from the copilot schema and nothing else; the executor impersonates it with EXECUTE AS USER.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE OR ALTER PROCEDURE copilot.usp_RecordAwardRecommendation
    @RfpId          varchar(12),
    @VendorId       varchar(8),          -- 'VND-nnnn'
    @Rationale      nvarchar(max),
    @WinningScore   decimal(6, 2) = NULL,
    @UnitPriceUsd   money,               -- unit price already converted to the RFP currency (USD)
    @LeadTimeWeeks  int,
    @EmployeeID     int = 261,           -- Purchasing Assistant in AdventureWorks
    @ShipMethodID   int = 1,             -- XRQ - TRUCK GROUND
    @RecordedBy     nvarchar(128) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @BusinessEntityID int = TRY_CONVERT(int, RIGHT(@VendorId, 4));
    IF @BusinessEntityID IS NULL OR NOT EXISTS (SELECT 1 FROM Purchasing.Vendor WHERE BusinessEntityID = @BusinessEntityID)
        THROW 51001, 'Vendor.NotFound: the vendor id is unknown.', 1;
    IF EXISTS (SELECT 1 FROM Procurement.RestrictedParty WHERE BusinessEntityID = @BusinessEntityID)
        THROW 51002, 'Award.VendorSanctioned: a restricted party cannot receive an award recommendation.', 1;

    DECLARE @ProductID int, @Quantity int, @Status varchar(10);
    SELECT @ProductID = ProductID, @Quantity = Quantity, @Status = Status FROM Procurement.Rfp WHERE RfpId = @RfpId;
    IF @ProductID IS NULL
        THROW 51003, 'Rfp.NotFound: the RFP id is unknown.', 1;
    IF @Status <> 'Open'
        THROW 51004, 'Rfp.Closed: the RFP is not open.', 1;
    IF @Quantity > 32767
        THROW 51005, 'Rfp.QuantityTooLarge: purchase order lines hold at most 32767 units.', 1;
    IF @UnitPriceUsd < 0 OR @LeadTimeWeeks <= 0
        THROW 51006, 'Award.InvalidArguments: unit price must be >= 0 and lead time > 0.', 1;

    BEGIN TRANSACTION;

    DECLARE @OrderDate datetime = CONVERT(date, GETDATE());
    DECLARE @DueDate  datetime = DATEADD(week, @LeadTimeWeeks, @OrderDate);

    INSERT INTO Purchasing.PurchaseOrderHeader (RevisionNumber, Status, EmployeeID, VendorID, ShipMethodID, OrderDate, ShipDate, SubTotal, TaxAmt, Freight, ModifiedDate)
    VALUES (0, 1 /* Pending */, @EmployeeID, @BusinessEntityID, @ShipMethodID, @OrderDate, @DueDate, 0, 0, 0, GETDATE());
    DECLARE @PurchaseOrderID int = SCOPE_IDENTITY();

    -- The AdventureWorks trigger iPurchaseOrderDetail recomputes the header SubTotal from the lines.
    INSERT INTO Purchasing.PurchaseOrderDetail (PurchaseOrderID, DueDate, OrderQty, ProductID, UnitPrice, ReceivedQty, RejectedQty, ModifiedDate)
    VALUES (@PurchaseOrderID, @DueDate, @Quantity, @ProductID, @UnitPriceUsd, 0, 0, GETDATE());

    INSERT INTO Procurement.AwardRecommendation (RfpId, BusinessEntityID, PurchaseOrderID, WinningScore, Rationale, RecordedBy)
    VALUES (@RfpId, @BusinessEntityID, @PurchaseOrderID, @WinningScore, @Rationale, COALESCE(@RecordedBy, SUSER_SNAME()));
    DECLARE @AwardRecommendationID int = SCOPE_IDENTITY();

    COMMIT TRANSACTION;

    SELECT @PurchaseOrderID AS PurchaseOrderID, @AwardRecommendationID AS AwardRecommendationID,
           (SELECT TotalDue FROM Purchasing.PurchaseOrderHeader WHERE PurchaseOrderID = @PurchaseOrderID) AS TotalDue;
END;
GO

-- Least privilege for free-form read queries: a database user without a login, SELECT on the copilot schema only.
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'copilot_reader')
    CREATE USER copilot_reader WITHOUT LOGIN;
GO
GRANT SELECT ON SCHEMA::copilot TO copilot_reader;
GO
-- The application connection (dbo) impersonates copilot_reader around each query_readonly call.
GRANT IMPERSONATE ON USER::copilot_reader TO public;
GO

IF NOT EXISTS (SELECT 1 FROM Procurement.SchemaMigration WHERE ScriptName = '0003_copilot_procedures_and_security.sql')
    INSERT INTO Procurement.SchemaMigration (ScriptName) VALUES ('0003_copilot_procedures_and_security.sql');
GO
