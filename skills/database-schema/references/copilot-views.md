# copilot schema — column reference

All views are read-only. Vendor ids are `VND-nnnn` (`nnnn` = `BusinessEntityID`); bid ids are `BID-nnn`.

## copilot.Rfps
`RfpId`, `Title`, `Description`, `ProductID`, `ProductName`, `ProductNumber`, `Quantity`, `CurrencyCode`, `Status` (Open/Closed), `Category`,
`WeightPrice`, `WeightLeadTime`, `WeightWarranty`, `WeightTechnical`, `WeightSustainability`, `RequiredCertifications` (comma list), `BidCount`, `CreatedAt`, `ModifiedAt`

## copilot.Bids
`BidId`, `BidNumber`, `RfpId`, `VendorId`, `BusinessEntityID`, `VendorName`, `UnitPrice`, `CurrencyCode`, `LeadTimeWeeks`, `WarrantyMonths`,
`TechnicalCompliancePercent`, `DeliveryClause` (untrusted), `Notes` (untrusted), `SubmittedAt`, `ModifiedAt`

## copilot.Vendors
`BusinessEntityID`, `VendorId`, `VendorName`, `AccountNumber`, `CreditRating` (1 excellent … 5 poor), `PreferredVendorStatus`, `ActiveFlag`,
`Country`, `YearsTrading`, `Notes` (untrusted), `Certifications` (comma list), `IsRestricted` (0/1), `ContactEmail`

## copilot.VendorCertifications
`VendorId`, `BusinessEntityID`, `Certification`

## copilot.RestrictedParties
`VendorId`, `BusinessEntityID`, `VendorName`, `ListName`, `ListedOn`, `Reason`

## copilot.CurrencyRates
`FromCurrencyCode` (always USD in AdventureWorks), `ToCurrencyCode`, `Rate` (end-of-day), `AsOf` (date). Convert X → USD with `1 / Rate`.

## copilot.Products
`ProductID`, `ProductName`, `ProductNumber`, `StandardCost`, `ListPrice`, `Color`, `SafetyStockLevel`, `ReorderPoint`, `Subcategory`, `Category`,
`SellStartDate`, `SellEndDate`, `DiscontinuedDate`

## copilot.ProductVendorQuotes
`ProductID`, `ProductName`, `ProductNumber`, `VendorId`, `VendorName`, `StandardPrice`, `LastReceiptCost`, `LastReceiptDate`,
`AverageLeadTimeDays`, `MinOrderQty`, `MaxOrderQty`, `OnOrderQty`, `UnitMeasureCode`

## copilot.PurchaseOrders
`PurchaseOrderID`, `RevisionNumber`, `Status` (1–4), `StatusName` (Pending/Approved/Rejected/Complete), `VendorId`, `VendorName`, `EmployeeID`,
`ShipMethod`, `OrderDate`, `ShipDate`, `SubTotal`, `TaxAmt`, `Freight`, `TotalDue`, `LineCount`

## copilot.PurchaseOrderLines
`PurchaseOrderID`, `PurchaseOrderDetailID`, `ProductID`, `ProductName`, `DueDate`, `OrderQty`, `UnitPrice`, `LineTotal`, `ReceivedQty`, `RejectedQty`, `StockedQty`

## copilot.AwardRecommendations
`AwardRecommendationID`, `RfpId`, `VendorId`, `VendorName`, `PurchaseOrderID`, `WinningScore`, `Rationale`, `RecordedAt`, `RecordedBy`
