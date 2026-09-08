-- reset-demo-awards.sql
-- Removes everything the copilot wrote to AdventureWorks during demos: the pending purchase orders created by
-- copilot.usp_RecordAwardRecommendation (header + lines) and the Procurement.AwardRecommendation rows that point at them.
-- Seed data (RFPs, bids, vendor profiles, restricted parties) is left untouched; re-run the migrations to restore it.
-- Usage: sqlcmd -S "localhost\SQLEXPRESS" -E -d AdventureWorks2019 -C -i database/maintenance/reset-demo-awards.sql
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

DECLARE @orders TABLE (PurchaseOrderID int PRIMARY KEY);
INSERT INTO @orders (PurchaseOrderID)
SELECT DISTINCT PurchaseOrderID FROM Procurement.AwardRecommendation WHERE PurchaseOrderID IS NOT NULL;

DELETE FROM Procurement.AwardRecommendation;
DELETE d FROM Purchasing.PurchaseOrderDetail d JOIN @orders o ON o.PurchaseOrderID = d.PurchaseOrderID;
DELETE h FROM Purchasing.PurchaseOrderHeader h JOIN @orders o ON o.PurchaseOrderID = h.PurchaseOrderID;

COMMIT TRANSACTION;

SELECT COUNT(*) AS PurchaseOrdersRemoved FROM @orders;
GO
