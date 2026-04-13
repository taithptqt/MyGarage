SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Quotations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Quotations
    (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Code VARCHAR(50) NOT NULL,
        ServiceOrderId BIGINT NOT NULL,
        Title NVARCHAR(255) NULL,
        QuotationType VARCHAR(30) NOT NULL CONSTRAINT DF_Quotations_QuotationType DEFAULT('NORMAL'),
        VersionNo INT NOT NULL,
        SubTotalAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Quotations_SubTotalAmount DEFAULT(0),
        TotalDiscount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Quotations_TotalDiscount DEFAULT(0),
        TaxAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Quotations_TaxAmount DEFAULT(0),
        FinalAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Quotations_FinalAmount DEFAULT(0),
        TotalInsuranceAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Quotations_TotalInsuranceAmount DEFAULT(0),
        TotalCustomerAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Quotations_TotalCustomerAmount DEFAULT(0),
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_Quotations_Status DEFAULT('DRAFT'),
        ValidUntil DATETIME2 NULL,
        ServiceAdvisorId BIGINT NULL,
        ApprovedBy BIGINT NULL,
        ApprovedAt DATETIME2 NULL,
        RejectReason NVARCHAR(500) NULL,
        CustomerNotes NVARCHAR(1000) NULL,
        InternalNotes NVARCHAR(1000) NULL,
        IsAppliedToServiceOrder BIT NOT NULL CONSTRAINT DF_Quotations_IsAppliedToServiceOrder DEFAULT(0),
        AppliedBy BIGINT NULL,
        AppliedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NULL CONSTRAINT DF_Quotations_CreatedAt DEFAULT(GETDATE()),
        CreatedBy BIGINT NULL,
        UpdatedAt DATETIME2 NULL CONSTRAINT DF_Quotations_UpdatedAt DEFAULT(GETDATE()),
        UpdatedBy BIGINT NULL
    );

    CREATE UNIQUE INDEX UQ_Quotations_Code ON dbo.Quotations(Code);
    CREATE UNIQUE INDEX IX_Quotations_ServiceOrderId_VersionNo ON dbo.Quotations(ServiceOrderId, VersionNo);
    CREATE INDEX IX_Quotations_ServiceOrderId_Status_CreatedAt ON dbo.Quotations(ServiceOrderId, Status, CreatedAt DESC);

    ALTER TABLE dbo.Quotations ADD CONSTRAINT FK_Quotations_ServiceOrders FOREIGN KEY(ServiceOrderId) REFERENCES dbo.ServiceOrders(Id);
    ALTER TABLE dbo.Quotations ADD CONSTRAINT FK_Quotations_ServiceAdvisor FOREIGN KEY(ServiceAdvisorId) REFERENCES dbo.Staffs(Id);
    ALTER TABLE dbo.Quotations ADD CONSTRAINT FK_Quotations_ApprovedBy FOREIGN KEY(ApprovedBy) REFERENCES dbo.Staffs(Id);
    ALTER TABLE dbo.Quotations ADD CONSTRAINT FK_Quotations_AppliedBy FOREIGN KEY(AppliedBy) REFERENCES dbo.Staffs(Id);
    ALTER TABLE dbo.Quotations ADD CONSTRAINT FK_Quotations_CreatedBy FOREIGN KEY(CreatedBy) REFERENCES dbo.Staffs(Id);
    ALTER TABLE dbo.Quotations ADD CONSTRAINT FK_Quotations_UpdatedBy FOREIGN KEY(UpdatedBy) REFERENCES dbo.Staffs(Id);

    ALTER TABLE dbo.Quotations ADD CONSTRAINT CK_Quotations_Status CHECK (Status IN ('DRAFT','WAITING_APPROVAL','APPROVED','REJECTED','SUPERSEDED'));
    ALTER TABLE dbo.Quotations ADD CONSTRAINT CK_Quotations_QuotationType CHECK (QuotationType IN ('NORMAL','INSURANCE','MIXED'));
END;

IF OBJECT_ID(N'dbo.QuotationDetails', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuotationDetails
    (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        QuotationId BIGINT NOT NULL,
        ServiceOrderId BIGINT NOT NULL,
        DiagnoseRecommendationId BIGINT NULL,
        ItemType VARCHAR(50) NOT NULL,
        ItemId BIGINT NULL,
        ItemName NVARCHAR(255) NOT NULL,
        Description NVARCHAR(1000) NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        UnitPriceAtTime DECIMAL(18,2) NOT NULL,
        CostPriceAtTime DECIMAL(18,2) NULL,
        SubTotal DECIMAL(18,2) NOT NULL,
        DiscountValue DECIMAL(18,2) NOT NULL CONSTRAINT DF_QuotationDetails_DiscountValue DEFAULT(0),
        TaxRate DECIMAL(5,2) NOT NULL,
        TaxAmount DECIMAL(18,2) NOT NULL,
        FinalAmount DECIMAL(18,2) NOT NULL,
        PayerType VARCHAR(50) NOT NULL,
        InsuranceCompanyId BIGINT NULL,
        InsuranceRequestedAmount DECIMAL(18,2) NULL,
        CustomerCoPayAmount DECIMAL(18,2) NULL,
        ApprovalStatus VARCHAR(20) NOT NULL CONSTRAINT DF_QuotationDetails_ApprovalStatus DEFAULT('PENDING'),
        ApprovedBy BIGINT NULL,
        ApprovedAt DATETIME2 NULL,
        RejectReason NVARCHAR(500) NULL,
        IsBlocking BIT NOT NULL CONSTRAINT DF_QuotationDetails_IsBlocking DEFAULT(1),
        EstimatedMinutes INT NULL,
        IsReturnOldPart BIT NOT NULL CONSTRAINT DF_QuotationDetails_IsReturnOldPart DEFAULT(0),
        CreatedAt DATETIME2 NULL CONSTRAINT DF_QuotationDetails_CreatedAt DEFAULT(GETDATE()),
        CreatedBy BIGINT NULL,
        UpdatedAt DATETIME2 NULL CONSTRAINT DF_QuotationDetails_UpdatedAt DEFAULT(GETDATE()),
        UpdatedBy BIGINT NULL
    );

    CREATE INDEX IX_QuotationDetails_QuotationId_ApprovalStatus ON dbo.QuotationDetails(QuotationId, ApprovalStatus);
    CREATE INDEX IX_QuotationDetails_ServiceOrderId_DiagnoseRecommendationId ON dbo.QuotationDetails(ServiceOrderId, DiagnoseRecommendationId);

    ALTER TABLE dbo.QuotationDetails ADD CONSTRAINT FK_QuotationDetails_Quotations FOREIGN KEY(QuotationId) REFERENCES dbo.Quotations(Id) ON DELETE CASCADE;
    ALTER TABLE dbo.QuotationDetails ADD CONSTRAINT FK_QuotationDetails_ServiceOrders FOREIGN KEY(ServiceOrderId) REFERENCES dbo.ServiceOrders(Id);
    ALTER TABLE dbo.QuotationDetails ADD CONSTRAINT FK_QuotationDetails_DiagnoseRecommendations FOREIGN KEY(DiagnoseRecommendationId) REFERENCES dbo.DiagnoseRecommendations(Id);
    ALTER TABLE dbo.QuotationDetails ADD CONSTRAINT FK_QuotationDetails_InsuranceCompanies FOREIGN KEY(InsuranceCompanyId) REFERENCES dbo.InsuranceCompanies(Id);
    ALTER TABLE dbo.QuotationDetails ADD CONSTRAINT FK_QuotationDetails_ApprovedBy FOREIGN KEY(ApprovedBy) REFERENCES dbo.Staffs(Id);
    ALTER TABLE dbo.QuotationDetails ADD CONSTRAINT FK_QuotationDetails_CreatedBy FOREIGN KEY(CreatedBy) REFERENCES dbo.Staffs(Id);
    ALTER TABLE dbo.QuotationDetails ADD CONSTRAINT FK_QuotationDetails_UpdatedBy FOREIGN KEY(UpdatedBy) REFERENCES dbo.Staffs(Id);

    ALTER TABLE dbo.QuotationDetails ADD CONSTRAINT CK_QuotationDetails_ApprovalStatus CHECK (ApprovalStatus IN ('PENDING','APPROVED','REJECTED'));
END;

IF COL_LENGTH('dbo.ServiceOrderDetails', 'QuotationDetailId') IS NULL
BEGIN
    ALTER TABLE dbo.ServiceOrderDetails ADD QuotationDetailId BIGINT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ServiceOrderDetails')
      AND name = N'IX_ServiceOrderDetails_QuotationDetailId')
BEGIN
    CREATE INDEX IX_ServiceOrderDetails_QuotationDetailId ON dbo.ServiceOrderDetails(QuotationDetailId);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_ServiceOrderDetails_QuotationDetails')
BEGIN
    ALTER TABLE dbo.ServiceOrderDetails
        ADD CONSTRAINT FK_ServiceOrderDetails_QuotationDetails
        FOREIGN KEY (QuotationDetailId) REFERENCES dbo.QuotationDetails(Id);
END;

IF COL_LENGTH('dbo.ServiceOrders', 'ValidUntil') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ServiceOrders DROP COLUMN ValidUntil;
END;

PRINT N'[DONE] Quotation schema migration completed.';
