-- 0001_procurement_schema.sql
-- Creates the Procurement schema on top of AdventureWorks2019: the tables the Procurement Copilot owns.
-- AdventureWorks base tables are never altered. Idempotent: safe to re-run.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF SCHEMA_ID('Procurement') IS NULL
    EXEC('CREATE SCHEMA Procurement AUTHORIZATION dbo');
GO

-- Migration bookkeeping: every script records itself here; the runner skips recorded scripts.
IF OBJECT_ID('Procurement.SchemaMigration', 'U') IS NULL
CREATE TABLE Procurement.SchemaMigration
(
    ScriptName  nvarchar(200) NOT NULL CONSTRAINT PK_SchemaMigration PRIMARY KEY,
    AppliedAt   datetime2(0)  NOT NULL CONSTRAINT DF_SchemaMigration_AppliedAt DEFAULT SYSUTCDATETIME(),
    AppliedBy   nvarchar(128) NOT NULL CONSTRAINT DF_SchemaMigration_AppliedBy DEFAULT SUSER_SNAME()
);
GO

IF OBJECT_ID('Procurement.Rfp', 'U') IS NULL
CREATE TABLE Procurement.Rfp
(
    RfpId                 varchar(12)   NOT NULL CONSTRAINT PK_Rfp PRIMARY KEY,
    Title                 nvarchar(200) NOT NULL,
    Description           nvarchar(max) NOT NULL,
    ProductID             int           NOT NULL CONSTRAINT FK_Rfp_Product REFERENCES Production.Product (ProductID),
    Quantity              int           NOT NULL CONSTRAINT CK_Rfp_Quantity CHECK (Quantity > 0),
    CurrencyCode          nchar(3)      NOT NULL CONSTRAINT FK_Rfp_Currency REFERENCES Sales.Currency (CurrencyCode),
    Status                varchar(10)   NOT NULL CONSTRAINT CK_Rfp_Status CHECK (Status IN ('Open', 'Closed')),
    Category              nvarchar(50)  NOT NULL,
    WeightPrice           decimal(5, 2) NOT NULL CONSTRAINT CK_Rfp_WeightPrice CHECK (WeightPrice >= 0),
    WeightLeadTime        decimal(5, 2) NOT NULL CONSTRAINT CK_Rfp_WeightLeadTime CHECK (WeightLeadTime >= 0),
    WeightWarranty        decimal(5, 2) NOT NULL CONSTRAINT CK_Rfp_WeightWarranty CHECK (WeightWarranty >= 0),
    WeightTechnical       decimal(5, 2) NOT NULL CONSTRAINT CK_Rfp_WeightTechnical CHECK (WeightTechnical >= 0),
    WeightSustainability  decimal(5, 2) NOT NULL CONSTRAINT CK_Rfp_WeightSustainability CHECK (WeightSustainability >= 0),
    CreatedAt             datetime2(0)  NOT NULL CONSTRAINT DF_Rfp_CreatedAt DEFAULT SYSUTCDATETIME(),
    ModifiedAt            datetime2(0)  NOT NULL CONSTRAINT DF_Rfp_ModifiedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Rfp_RfpId CHECK (RfpId LIKE '[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9]')
);
GO

IF OBJECT_ID('Procurement.RfpRequiredCertification', 'U') IS NULL
CREATE TABLE Procurement.RfpRequiredCertification
(
    RfpId          varchar(12)  NOT NULL CONSTRAINT FK_RfpRequiredCertification_Rfp REFERENCES Procurement.Rfp (RfpId) ON DELETE CASCADE,
    Certification  nvarchar(50) NOT NULL,
    CONSTRAINT PK_RfpRequiredCertification PRIMARY KEY (RfpId, Certification)
);
GO

-- Copilot-specific facts about an AdventureWorks vendor (the vendor row itself stays in Purchasing.Vendor).
IF OBJECT_ID('Procurement.VendorProfile', 'U') IS NULL
CREATE TABLE Procurement.VendorProfile
(
    BusinessEntityID  int           NOT NULL CONSTRAINT PK_VendorProfile PRIMARY KEY
                                             CONSTRAINT FK_VendorProfile_Vendor REFERENCES Purchasing.Vendor (BusinessEntityID),
    CountryOverride   nvarchar(50)  NULL,
    YearsTrading      int           NULL CONSTRAINT CK_VendorProfile_YearsTrading CHECK (YearsTrading IS NULL OR YearsTrading >= 0),
    Notes             nvarchar(max) NULL,   -- vendor-authored, treated as untrusted data by the agent
    ModifiedAt        datetime2(0)  NOT NULL CONSTRAINT DF_VendorProfile_ModifiedAt DEFAULT SYSUTCDATETIME()
);
GO

IF OBJECT_ID('Procurement.VendorCertification', 'U') IS NULL
CREATE TABLE Procurement.VendorCertification
(
    BusinessEntityID  int          NOT NULL CONSTRAINT FK_VendorCertification_Vendor REFERENCES Purchasing.Vendor (BusinessEntityID),
    Certification     nvarchar(50) NOT NULL,
    CONSTRAINT PK_VendorCertification PRIMARY KEY (BusinessEntityID, Certification)
);
GO

IF OBJECT_ID('Procurement.Bid', 'U') IS NULL
CREATE TABLE Procurement.Bid
(
    BidNumber                   int           NOT NULL IDENTITY(1, 1) CONSTRAINT PK_Bid PRIMARY KEY,
    BidId                       AS ('BID-' + RIGHT('000' + CONVERT(varchar(3), BidNumber), 3)) PERSISTED,
    RfpId                       varchar(12)   NOT NULL CONSTRAINT FK_Bid_Rfp REFERENCES Procurement.Rfp (RfpId) ON DELETE CASCADE,
    BusinessEntityID            int           NOT NULL CONSTRAINT FK_Bid_Vendor REFERENCES Purchasing.Vendor (BusinessEntityID),
    UnitPrice                   money         NOT NULL CONSTRAINT CK_Bid_UnitPrice CHECK (UnitPrice >= 0),
    CurrencyCode                nchar(3)      NOT NULL CONSTRAINT FK_Bid_Currency REFERENCES Sales.Currency (CurrencyCode),
    LeadTimeWeeks               int           NOT NULL CONSTRAINT CK_Bid_LeadTimeWeeks CHECK (LeadTimeWeeks > 0),
    WarrantyMonths              int           NOT NULL CONSTRAINT CK_Bid_WarrantyMonths CHECK (WarrantyMonths >= 0),
    TechnicalCompliancePercent  decimal(5, 2) NOT NULL CONSTRAINT CK_Bid_Technical CHECK (TechnicalCompliancePercent BETWEEN 0 AND 100),
    DeliveryClause              nvarchar(max) NOT NULL CONSTRAINT DF_Bid_DeliveryClause DEFAULT (''),   -- vendor-authored, untrusted
    Notes                       nvarchar(max) NOT NULL CONSTRAINT DF_Bid_Notes DEFAULT (''),            -- vendor-authored, untrusted
    SubmittedAt                 datetime2(0)  NOT NULL CONSTRAINT DF_Bid_SubmittedAt DEFAULT SYSUTCDATETIME(),
    ModifiedAt                  datetime2(0)  NOT NULL CONSTRAINT DF_Bid_ModifiedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Bid_BidId UNIQUE (BidId),
    CONSTRAINT UQ_Bid_RfpVendor UNIQUE (RfpId, BusinessEntityID)
);
GO

IF OBJECT_ID('Procurement.RestrictedParty', 'U') IS NULL
CREATE TABLE Procurement.RestrictedParty
(
    BusinessEntityID  int           NOT NULL CONSTRAINT PK_RestrictedParty PRIMARY KEY
                                             CONSTRAINT FK_RestrictedParty_Vendor REFERENCES Purchasing.Vendor (BusinessEntityID),
    ListName          nvarchar(100) NOT NULL,
    ListedOn          date          NOT NULL,
    Reason            nvarchar(400) NOT NULL
);
GO

IF OBJECT_ID('Procurement.AwardRecommendation', 'U') IS NULL
CREATE TABLE Procurement.AwardRecommendation
(
    AwardRecommendationID  int           NOT NULL IDENTITY(1, 1) CONSTRAINT PK_AwardRecommendation PRIMARY KEY,
    RfpId                  varchar(12)   NOT NULL CONSTRAINT FK_AwardRecommendation_Rfp REFERENCES Procurement.Rfp (RfpId),
    BusinessEntityID       int           NOT NULL CONSTRAINT FK_AwardRecommendation_Vendor REFERENCES Purchasing.Vendor (BusinessEntityID),
    PurchaseOrderID        int           NULL CONSTRAINT FK_AwardRecommendation_PurchaseOrder REFERENCES Purchasing.PurchaseOrderHeader (PurchaseOrderID),
    WinningScore           decimal(6, 2) NULL,
    Rationale              nvarchar(max) NOT NULL,
    RecordedAt             datetime2(0)  NOT NULL CONSTRAINT DF_AwardRecommendation_RecordedAt DEFAULT SYSUTCDATETIME(),
    RecordedBy             nvarchar(128) NOT NULL CONSTRAINT DF_AwardRecommendation_RecordedBy DEFAULT SUSER_SNAME()
);
GO

IF NOT EXISTS (SELECT 1 FROM Procurement.SchemaMigration WHERE ScriptName = '0001_procurement_schema.sql')
    INSERT INTO Procurement.SchemaMigration (ScriptName) VALUES ('0001_procurement_schema.sql');
GO
