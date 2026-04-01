/*
    Voucher Phase 1 upgrade (idempotent)
    Target: SQL Server
    Notes:
    - Keep backward compatibility with existing Vouchers flow.
    - Add governance + reporting + approval-ready structures.
    - Safe to run multiple times.
*/

SET NOCOUNT ON;

/* =========================================================
   1) Campaign master
   ========================================================= */
IF OBJECT_ID(N'dbo.VoucherCampaigns', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VoucherCampaigns
    (
        Id BIGINT PRIMARY KEY,
        CampaignCode VARCHAR(50) NULL,
        Name NVARCHAR(255) NOT NULL,
        Objective NVARCHAR(500) NULL,
        Description NVARCHAR(MAX) NULL,
        StartDate DATETIME2 NOT NULL,
        EndDate DATETIME2 NOT NULL,
        BudgetLimit DECIMAL(18, 2) NULL,
        BudgetUsed DECIMAL(18, 2) NOT NULL CONSTRAINT DF_VoucherCampaigns_BudgetUsed DEFAULT (0),
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_VoucherCampaigns_Status DEFAULT ('DRAFT'),
        OwnerStaffId BIGINT NULL REFERENCES dbo.Staffs(Id),
        IsActive BIT NOT NULL CONSTRAINT DF_VoucherCampaigns_IsActive DEFAULT (1),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherCampaigns_CreatedAt DEFAULT (GETDATE()),
        CreatedBy BIGINT NULL REFERENCES dbo.Staffs(Id),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherCampaigns_UpdatedAt DEFAULT (GETDATE()),
        UpdatedBy BIGINT NULL REFERENCES dbo.Staffs(Id),
        IsDeleted BIT NOT NULL CONSTRAINT DF_VoucherCampaigns_IsDeleted DEFAULT (0),
        DeletedAt DATETIME2 NULL,
        DeletedBy BIGINT NULL REFERENCES dbo.Staffs(Id)
    );

    ALTER TABLE dbo.VoucherCampaigns WITH CHECK
    ADD CONSTRAINT CHK_VoucherCampaigns_Status
    CHECK (Status IN ('DRAFT', 'PENDING_APPROVAL', 'SCHEDULED', 'ACTIVE', 'PAUSED', 'ENDED', 'ARCHIVED'));

    ALTER TABLE dbo.VoucherCampaigns WITH CHECK
    ADD CONSTRAINT CHK_VoucherCampaigns_DateRange
    CHECK (EndDate >= StartDate);
END;

IF OBJECT_ID(N'dbo.VoucherCampaigns', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_VoucherCampaigns_CampaignCode' AND object_id = OBJECT_ID(N'dbo.VoucherCampaigns'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_VoucherCampaigns_CampaignCode
        ON dbo.VoucherCampaigns(CampaignCode)
        WHERE CampaignCode IS NOT NULL;
END;

/* =========================================================
   2) Upgrade dbo.Vouchers
   ========================================================= */
IF COL_LENGTH(N'dbo.Vouchers', N'VoucherName') IS NULL
    ALTER TABLE dbo.Vouchers ADD VoucherName NVARCHAR(255) NULL;

IF COL_LENGTH(N'dbo.Vouchers', N'Description') IS NULL
    ALTER TABLE dbo.Vouchers ADD Description NVARCHAR(1000) NULL;

IF COL_LENGTH(N'dbo.Vouchers', N'Status') IS NULL
    ALTER TABLE dbo.Vouchers ADD Status VARCHAR(30) NULL;

IF COL_LENGTH(N'dbo.Vouchers', N'CampaignId') IS NULL
    ALTER TABLE dbo.Vouchers ADD CampaignId BIGINT NULL;

IF COL_LENGTH(N'dbo.Vouchers', N'MaxUsesPerCustomer') IS NULL
    ALTER TABLE dbo.Vouchers ADD MaxUsesPerCustomer INT NOT NULL CONSTRAINT DF_Vouchers_MaxUsesPerCustomer DEFAULT (0);

IF COL_LENGTH(N'dbo.Vouchers', N'MaxUsesPerVehicle') IS NULL
    ALTER TABLE dbo.Vouchers ADD MaxUsesPerVehicle INT NOT NULL CONSTRAINT DF_Vouchers_MaxUsesPerVehicle DEFAULT (0);

IF COL_LENGTH(N'dbo.Vouchers', N'IssuedCount') IS NULL
    ALTER TABLE dbo.Vouchers ADD IssuedCount INT NOT NULL CONSTRAINT DF_Vouchers_IssuedCount DEFAULT (0);

IF COL_LENGTH(N'dbo.Vouchers', N'RedeemedCount') IS NULL
    ALTER TABLE dbo.Vouchers ADD RedeemedCount INT NOT NULL CONSTRAINT DF_Vouchers_RedeemedCount DEFAULT (0);

IF COL_LENGTH(N'dbo.Vouchers', N'BudgetLimit') IS NULL
    ALTER TABLE dbo.Vouchers ADD BudgetLimit DECIMAL(18, 2) NULL;

IF COL_LENGTH(N'dbo.Vouchers', N'BudgetUsed') IS NULL
    ALTER TABLE dbo.Vouchers ADD BudgetUsed DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Vouchers_BudgetUsed DEFAULT (0);

IF COL_LENGTH(N'dbo.Vouchers', N'FirstUseOnly') IS NULL
    ALTER TABLE dbo.Vouchers ADD FirstUseOnly BIT NOT NULL CONSTRAINT DF_Vouchers_FirstUseOnly DEFAULT (0);

IF COL_LENGTH(N'dbo.Vouchers', N'ExcludeDiscountedItems') IS NULL
    ALTER TABLE dbo.Vouchers ADD ExcludeDiscountedItems BIT NOT NULL CONSTRAINT DF_Vouchers_ExcludeDiscountedItems DEFAULT (0);

IF COL_LENGTH(N'dbo.Vouchers', N'AllowedPaymentMethods') IS NULL
    ALTER TABLE dbo.Vouchers ADD AllowedPaymentMethods NVARCHAR(MAX) NULL;

IF COL_LENGTH(N'dbo.Vouchers', N'AllowedChannels') IS NULL
    ALTER TABLE dbo.Vouchers ADD AllowedChannels NVARCHAR(MAX) NULL;

IF COL_LENGTH(N'dbo.Vouchers', N'ValidDaysOfWeek') IS NULL
    ALTER TABLE dbo.Vouchers ADD ValidDaysOfWeek VARCHAR(30) NULL;

IF COL_LENGTH(N'dbo.Vouchers', N'Timezone') IS NULL
    ALTER TABLE dbo.Vouchers ADD Timezone VARCHAR(64) NOT NULL CONSTRAINT DF_Vouchers_Timezone DEFAULT ('Asia/Ho_Chi_Minh');

IF COL_LENGTH(N'dbo.Vouchers', N'ApprovalRequired') IS NULL
    ALTER TABLE dbo.Vouchers ADD ApprovalRequired BIT NOT NULL CONSTRAINT DF_Vouchers_ApprovalRequired DEFAULT (0);

IF COL_LENGTH(N'dbo.Vouchers', N'ApprovalThresholdAmount') IS NULL
    ALTER TABLE dbo.Vouchers ADD ApprovalThresholdAmount DECIMAL(18, 2) NULL;

IF COL_LENGTH(N'dbo.Vouchers', N'Version') IS NULL
    ALTER TABLE dbo.Vouchers ADD Version ROWVERSION;

/* Backfill */
IF COL_LENGTH(N'dbo.Vouchers', N'VoucherName') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE dbo.Vouchers
        SET VoucherName = Code
        WHERE (VoucherName IS NULL OR LTRIM(RTRIM(VoucherName)) = N'''');';
END;

IF COL_LENGTH(N'dbo.Vouchers', N'Status') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE dbo.Vouchers
        SET Status =
            CASE
                WHEN IsDeleted = 1 THEN ''ARCHIVED''
                WHEN IsActive = 0 THEN ''PAUSED''
                WHEN EndDate < GETDATE() THEN ''EXPIRED''
                WHEN UsageLimit > 0 AND ISNULL(UsedCount, 0) >= UsageLimit THEN ''EXHAUSTED''
                WHEN StartDate > GETDATE() THEN ''SCHEDULED''
                ELSE ''ACTIVE''
            END
        WHERE Status IS NULL;';
END;

IF OBJECT_ID(N'dbo.CustomerVouchers', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Vouchers', N'IssuedCount') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE v
        SET v.IssuedCount = x.Cnt
        FROM dbo.Vouchers v
        OUTER APPLY
        (
            SELECT COUNT_BIG(1) AS Cnt
            FROM dbo.CustomerVouchers cv
            WHERE cv.VoucherId = v.Id
        ) x
        WHERE v.IssuedCount = 0;';
END;

IF COL_LENGTH(N'dbo.Vouchers', N'RedeemedCount') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE dbo.Vouchers
        SET RedeemedCount = ISNULL(UsedCount, 0)
        WHERE RedeemedCount = 0;';
END;

IF COL_LENGTH(N'dbo.Vouchers', N'VoucherName') IS NOT NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.Vouchers ALTER COLUMN VoucherName NVARCHAR(255) NOT NULL;';

IF COL_LENGTH(N'dbo.Vouchers', N'Status') IS NOT NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.Vouchers ALTER COLUMN Status VARCHAR(30) NOT NULL;';

/* Constraints */
IF COL_LENGTH(N'dbo.Vouchers', N'Status') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CHK_Vouchers_Status' AND parent_object_id = OBJECT_ID(N'dbo.Vouchers'))
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE dbo.Vouchers WITH NOCHECK
        ADD CONSTRAINT CHK_Vouchers_Status
        CHECK (Status IN (''DRAFT'', ''PENDING_APPROVAL'', ''SCHEDULED'', ''ACTIVE'', ''PAUSED'', ''EXHAUSTED'', ''EXPIRED'', ''REVOKED'', ''ARCHIVED''));';
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CHK_Vouchers_DateRange' AND parent_object_id = OBJECT_ID(N'dbo.Vouchers'))
BEGIN
    ALTER TABLE dbo.Vouchers WITH NOCHECK
    ADD CONSTRAINT CHK_Vouchers_DateRange CHECK (EndDate >= StartDate);
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CHK_Vouchers_Value_Positive' AND parent_object_id = OBJECT_ID(N'dbo.Vouchers'))
BEGIN
    ALTER TABLE dbo.Vouchers WITH NOCHECK
    ADD CONSTRAINT CHK_Vouchers_Value_Positive CHECK (Value > 0);
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CHK_Vouchers_UsageRange' AND parent_object_id = OBJECT_ID(N'dbo.Vouchers'))
BEGIN
    ALTER TABLE dbo.Vouchers WITH NOCHECK
    ADD CONSTRAINT CHK_Vouchers_UsageRange
    CHECK
    (
        (
            ISNULL(UsageLimit, 0) = 0
            AND ISNULL(UsedCount, 0) >= 0
        )
        OR
        (
            ISNULL(UsageLimit, 0) > 0
            AND ISNULL(UsedCount, 0) >= 0
            AND ISNULL(UsedCount, 0) <= UsageLimit
        )
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CHK_Vouchers_BudgetRange' AND parent_object_id = OBJECT_ID(N'dbo.Vouchers'))
   AND COL_LENGTH(N'dbo.Vouchers', N'BudgetUsed') IS NOT NULL
   AND COL_LENGTH(N'dbo.Vouchers', N'BudgetLimit') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE dbo.Vouchers WITH NOCHECK
        ADD CONSTRAINT CHK_Vouchers_BudgetRange CHECK (BudgetUsed >= 0 AND (BudgetLimit IS NULL OR BudgetLimit >= BudgetUsed));';
END;

IF OBJECT_ID(N'dbo.VoucherCampaigns', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Vouchers', N'CampaignId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Vouchers_VoucherCampaigns_CampaignId')
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE dbo.Vouchers
        ADD CONSTRAINT FK_Vouchers_VoucherCampaigns_CampaignId
        FOREIGN KEY (CampaignId) REFERENCES dbo.VoucherCampaigns(Id);';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vouchers_Status_Date' AND object_id = OBJECT_ID(N'dbo.Vouchers'))
   AND COL_LENGTH(N'dbo.Vouchers', N'Status') IS NOT NULL
   AND COL_LENGTH(N'dbo.Vouchers', N'VoucherName') IS NOT NULL
   AND COL_LENGTH(N'dbo.Vouchers', N'BudgetLimit') IS NOT NULL
   AND COL_LENGTH(N'dbo.Vouchers', N'BudgetUsed') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        CREATE NONCLUSTERED INDEX IX_Vouchers_Status_Date
            ON dbo.Vouchers(Status, StartDate, EndDate)
            INCLUDE (Code, VoucherName, Type, Value, UsageLimit, UsedCount, BudgetLimit, BudgetUsed);';
END;

IF COL_LENGTH(N'dbo.Vouchers', N'CampaignId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vouchers_CampaignId' AND object_id = OBJECT_ID(N'dbo.Vouchers'))
BEGIN
    EXEC sys.sp_executesql N'
        CREATE NONCLUSTERED INDEX IX_Vouchers_CampaignId
            ON dbo.Vouchers(CampaignId);';
END;

/* =========================================================
   3) Voucher rules and assignments
   ========================================================= */
IF OBJECT_ID(N'dbo.VoucherUsageRules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VoucherUsageRules
    (
        Id BIGINT PRIMARY KEY IDENTITY(1, 1),
        VoucherId BIGINT NOT NULL REFERENCES dbo.Vouchers(Id),
        RuleType VARCHAR(50) NOT NULL,
        Operator VARCHAR(20) NOT NULL CONSTRAINT DF_VoucherUsageRules_Operator DEFAULT ('IN'),
        RuleValue NVARCHAR(MAX) NOT NULL,
        IsExclude BIT NOT NULL CONSTRAINT DF_VoucherUsageRules_IsExclude DEFAULT (0),
        HardBlock BIT NOT NULL CONSTRAINT DF_VoucherUsageRules_HardBlock DEFAULT (1),
        Priority INT NOT NULL CONSTRAINT DF_VoucherUsageRules_Priority DEFAULT (100),
        IsActive BIT NOT NULL CONSTRAINT DF_VoucherUsageRules_IsActive DEFAULT (1),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherUsageRules_CreatedAt DEFAULT (GETDATE()),
        CreatedBy BIGINT NULL REFERENCES dbo.Staffs(Id),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherUsageRules_UpdatedAt DEFAULT (GETDATE()),
        UpdatedBy BIGINT NULL REFERENCES dbo.Staffs(Id)
    );

    ALTER TABLE dbo.VoucherUsageRules WITH CHECK
    ADD CONSTRAINT CHK_VoucherUsageRules_RuleType
    CHECK (RuleType IN
    (
        'BRANCH',
        'CUSTOMER_TIER',
        'VEHICLE_BRAND',
        'VEHICLE_MODEL',
        'SERVICE',
        'PRODUCT',
        'MIN_INVOICE',
        'BOOKING_CHANNEL',
        'PAYMENT_METHOD',
        'TIME_RANGE',
        'DAY_OF_WEEK',
        'FIRST_USE',
        'MAX_USE_PER_CUSTOMER',
        'MAX_USE_PER_VEHICLE'
    ));

    ALTER TABLE dbo.VoucherUsageRules WITH CHECK
    ADD CONSTRAINT CHK_VoucherUsageRules_Operator
    CHECK (Operator IN ('IN', 'NOT_IN', 'EQ', 'NEQ', 'GTE', 'LTE', 'BETWEEN'));
END;

IF OBJECT_ID(N'dbo.VoucherAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VoucherAssignments
    (
        Id BIGINT PRIMARY KEY IDENTITY(1, 1),
        VoucherId BIGINT NOT NULL REFERENCES dbo.Vouchers(Id),
        AssignmentType VARCHAR(50) NOT NULL,
        TargetId BIGINT NOT NULL,
        IsInclude BIT NOT NULL CONSTRAINT DF_VoucherAssignments_IsInclude DEFAULT (1),
        EffectiveFrom DATETIME2 NULL,
        EffectiveTo DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherAssignments_CreatedAt DEFAULT (GETDATE()),
        CreatedBy BIGINT NULL REFERENCES dbo.Staffs(Id),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherAssignments_UpdatedAt DEFAULT (GETDATE()),
        UpdatedBy BIGINT NULL REFERENCES dbo.Staffs(Id)
    );

    ALTER TABLE dbo.VoucherAssignments WITH CHECK
    ADD CONSTRAINT CHK_VoucherAssignments_AssignmentType
    CHECK (AssignmentType IN ('BRANCH', 'CUSTOMER', 'CUSTOMER_TIER', 'VEHICLE', 'VEHICLE_MODEL', 'VEHICLE_BRAND', 'SERVICE', 'PRODUCT'));

    ALTER TABLE dbo.VoucherAssignments WITH CHECK
    ADD CONSTRAINT CHK_VoucherAssignments_EffectiveRange
    CHECK (EffectiveFrom IS NULL OR EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom);

    CREATE UNIQUE NONCLUSTERED INDEX UQ_VoucherAssignments_UniqueRow
        ON dbo.VoucherAssignments(VoucherId, AssignmentType, TargetId, IsInclude);
END;

IF OBJECT_ID(N'dbo.VoucherConflictRules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VoucherConflictRules
    (
        Id BIGINT PRIMARY KEY IDENTITY(1, 1),
        VoucherId BIGINT NOT NULL REFERENCES dbo.Vouchers(Id),
        ConflictType VARCHAR(50) NOT NULL,
        ConflictTarget NVARCHAR(200) NULL,
        ActionType VARCHAR(20) NOT NULL CONSTRAINT DF_VoucherConflictRules_ActionType DEFAULT ('BLOCK'),
        MessageCode VARCHAR(100) NULL,
        MessageText NVARCHAR(500) NULL,
        Priority INT NOT NULL CONSTRAINT DF_VoucherConflictRules_Priority DEFAULT (100),
        IsActive BIT NOT NULL CONSTRAINT DF_VoucherConflictRules_IsActive DEFAULT (1),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherConflictRules_CreatedAt DEFAULT (GETDATE()),
        CreatedBy BIGINT NULL REFERENCES dbo.Staffs(Id),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherConflictRules_UpdatedAt DEFAULT (GETDATE()),
        UpdatedBy BIGINT NULL REFERENCES dbo.Staffs(Id)
    );

    ALTER TABLE dbo.VoucherConflictRules WITH CHECK
    ADD CONSTRAINT CHK_VoucherConflictRules_ActionType
    CHECK (ActionType IN ('BLOCK', 'WARN', 'REQUIRE_APPROVAL'));
END;

/* =========================================================
   4) Approval + redemption + audit
   ========================================================= */
IF OBJECT_ID(N'dbo.VoucherApprovalRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VoucherApprovalRequests
    (
        Id BIGINT PRIMARY KEY IDENTITY(1, 1),
        RequestType VARCHAR(80) NOT NULL,
        VoucherId BIGINT NULL REFERENCES dbo.Vouchers(Id),
        RedemptionId BIGINT NULL,
        BranchId BIGINT NULL REFERENCES dbo.Branches(Id),
        ServiceOrderId BIGINT NULL REFERENCES dbo.ServiceOrders(Id),
        RetailOrderId BIGINT NULL REFERENCES dbo.RetailOrders(Id),
        InvoiceId BIGINT NULL REFERENCES dbo.Invoices(Id),
        RequestedBy BIGINT NOT NULL REFERENCES dbo.Staffs(Id),
        ApproverId BIGINT NULL REFERENCES dbo.Staffs(Id),
        Status VARCHAR(20) NOT NULL CONSTRAINT DF_VoucherApprovalRequests_Status DEFAULT ('PENDING'),
        RiskLevel VARCHAR(20) NOT NULL CONSTRAINT DF_VoucherApprovalRequests_RiskLevel DEFAULT ('MEDIUM'),
        Reason NVARCHAR(MAX) NOT NULL,
        RejectReason NVARCHAR(MAX) NULL,
        RequestPayload NVARCHAR(MAX) NULL,
        ImpactAmount DECIMAL(18, 2) NULL,
        SLADeadline DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherApprovalRequests_CreatedAt DEFAULT (GETDATE()),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherApprovalRequests_UpdatedAt DEFAULT (GETDATE())
    );

    ALTER TABLE dbo.VoucherApprovalRequests WITH CHECK
    ADD CONSTRAINT CHK_VoucherApprovalRequests_RequestType
    CHECK (RequestType IN
    (
        'APPLY_OUTSIDE_RULE',
        'EXCEED_QUOTA',
        'EXPIRED_OVERRIDE',
        'MANUAL_ISSUE',
        'COMBINE_WITH_MANUAL_DISCOUNT',
        'DISCOUNT_OVER_LIMIT',
        'REVERSE_AFTER_PAYMENT',
        'EDIT_AFTER_ISSUED',
        'REVOKE_ACTIVE_VOUCHER'
    ));

    ALTER TABLE dbo.VoucherApprovalRequests WITH CHECK
    ADD CONSTRAINT CHK_VoucherApprovalRequests_Status
    CHECK (Status IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELED'));

    ALTER TABLE dbo.VoucherApprovalRequests WITH CHECK
    ADD CONSTRAINT CHK_VoucherApprovalRequests_RiskLevel
    CHECK (RiskLevel IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL'));
END;

IF OBJECT_ID(N'dbo.VoucherRedemptions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VoucherRedemptions
    (
        Id BIGINT PRIMARY KEY IDENTITY(1, 1),
        VoucherId BIGINT NOT NULL REFERENCES dbo.Vouchers(Id),
        VoucherCode VARCHAR(50) NOT NULL,
        CustomerId BIGINT NULL REFERENCES dbo.Customers(Id),
        VehicleId BIGINT NULL REFERENCES dbo.Vehicles(Id),
        BranchId BIGINT NULL REFERENCES dbo.Branches(Id),
        ServiceOrderId BIGINT NULL REFERENCES dbo.ServiceOrders(Id),
        RetailOrderId BIGINT NULL REFERENCES dbo.RetailOrders(Id),
        InvoiceId BIGINT NULL REFERENCES dbo.Invoices(Id),
        PaymentMethodId BIGINT NULL REFERENCES dbo.PaymentMethods(Id),
        ValidationStatus VARCHAR(30) NOT NULL CONSTRAINT DF_VoucherRedemptions_ValidationStatus DEFAULT ('VALIDATED'),
        RedemptionStatus VARCHAR(30) NOT NULL CONSTRAINT DF_VoucherRedemptions_RedemptionStatus DEFAULT ('REDEEMED'),
        FailReasonCode VARCHAR(100) NULL,
        EligibleAmount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_VoucherRedemptions_EligibleAmount DEFAULT (0),
        DiscountAmount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_VoucherRedemptions_DiscountAmount DEFAULT (0),
        BudgetImpact DECIMAL(18, 2) NOT NULL CONSTRAINT DF_VoucherRedemptions_BudgetImpact DEFAULT (0),
        AppliedBy BIGINT NULL REFERENCES dbo.Staffs(Id),
        ApprovedBy BIGINT NULL REFERENCES dbo.Staffs(Id),
        ApprovalRequestId BIGINT NULL REFERENCES dbo.VoucherApprovalRequests(Id),
        IdempotencyKey VARCHAR(100) NULL,
        AppliedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherRedemptions_AppliedAt DEFAULT (GETDATE()),
        RedeemedAt DATETIME2 NULL,
        ReversedAt DATETIME2 NULL,
        ReverseReason NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherRedemptions_CreatedAt DEFAULT (GETDATE())
    );

    ALTER TABLE dbo.VoucherRedemptions WITH CHECK
    ADD CONSTRAINT CHK_VoucherRedemptions_ValidationStatus
    CHECK (ValidationStatus IN ('VALIDATED', 'FAILED', 'APPROVAL_REQUIRED'));

    ALTER TABLE dbo.VoucherRedemptions WITH CHECK
    ADD CONSTRAINT CHK_VoucherRedemptions_RedemptionStatus
    CHECK (RedemptionStatus IN ('RESERVED', 'REDEEMED', 'REVERSED', 'CANCELED', 'FAILED'));

    ALTER TABLE dbo.VoucherRedemptions WITH CHECK
    ADD CONSTRAINT CHK_VoucherRedemptions_Discount
    CHECK (DiscountAmount >= 0 AND DiscountAmount <= EligibleAmount);
END;

IF OBJECT_ID(N'dbo.VoucherApprovalRequests', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.VoucherApprovalRequests', N'RedemptionId') IS NOT NULL
   AND OBJECT_ID(N'dbo.VoucherRedemptions', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VoucherApprovalRequests_VoucherRedemptions_RedemptionId')
BEGIN
    ALTER TABLE dbo.VoucherApprovalRequests WITH NOCHECK
    ADD CONSTRAINT FK_VoucherApprovalRequests_VoucherRedemptions_RedemptionId
    FOREIGN KEY (RedemptionId) REFERENCES dbo.VoucherRedemptions(Id);
END;

IF OBJECT_ID(N'dbo.VoucherAuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VoucherAuditLogs
    (
        Id BIGINT PRIMARY KEY IDENTITY(1, 1),
        VoucherId BIGINT NULL REFERENCES dbo.Vouchers(Id),
        EntityName VARCHAR(50) NOT NULL,
        EntityId BIGINT NOT NULL,
        Action VARCHAR(50) NOT NULL,
        ReasonCode VARCHAR(100) NULL,
        BeforeData NVARCHAR(MAX) NULL,
        AfterData NVARCHAR(MAX) NULL,
        PerformedBy BIGINT NULL REFERENCES dbo.Staffs(Id),
        BranchId BIGINT NULL REFERENCES dbo.Branches(Id),
        CorrelationId VARCHAR(100) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherAuditLogs_CreatedAt DEFAULT (GETDATE())
    );
END;

IF OBJECT_ID(N'dbo.VoucherCodeBatches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VoucherCodeBatches
    (
        Id BIGINT PRIMARY KEY IDENTITY(1, 1),
        VoucherId BIGINT NOT NULL REFERENCES dbo.Vouchers(Id),
        BatchCode VARCHAR(50) NOT NULL,
        SourceType VARCHAR(20) NOT NULL CONSTRAINT DF_VoucherCodeBatches_SourceType DEFAULT ('GENERATED'),
        TotalCount INT NOT NULL CONSTRAINT DF_VoucherCodeBatches_TotalCount DEFAULT (0),
        SuccessCount INT NOT NULL CONSTRAINT DF_VoucherCodeBatches_SuccessCount DEFAULT (0),
        DuplicateCount INT NOT NULL CONSTRAINT DF_VoucherCodeBatches_DuplicateCount DEFAULT (0),
        InvalidCount INT NOT NULL CONSTRAINT DF_VoucherCodeBatches_InvalidCount DEFAULT (0),
        FileName NVARCHAR(260) NULL,
        Status VARCHAR(20) NOT NULL CONSTRAINT DF_VoucherCodeBatches_Status DEFAULT ('COMPLETED'),
        CreatedBy BIGINT NULL REFERENCES dbo.Staffs(Id),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_VoucherCodeBatches_CreatedAt DEFAULT (GETDATE())
    );

    ALTER TABLE dbo.VoucherCodeBatches WITH CHECK
    ADD CONSTRAINT CHK_VoucherCodeBatches_SourceType CHECK (SourceType IN ('GENERATED', 'IMPORTED'));

    ALTER TABLE dbo.VoucherCodeBatches WITH CHECK
    ADD CONSTRAINT CHK_VoucherCodeBatches_Status CHECK (Status IN ('PENDING', 'RUNNING', 'COMPLETED', 'FAILED'));

    CREATE UNIQUE NONCLUSTERED INDEX UQ_VoucherCodeBatches_BatchCode ON dbo.VoucherCodeBatches(BatchCode);
END;

/* =========================================================
   5) Useful indexes
   ========================================================= */
IF OBJECT_ID(N'dbo.ServiceOrderDiscounts', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ServiceOrderDiscounts_VoucherId' AND object_id = OBJECT_ID(N'dbo.ServiceOrderDiscounts'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ServiceOrderDiscounts_VoucherId
        ON dbo.ServiceOrderDiscounts(VoucherId)
        WHERE VoucherId IS NOT NULL;
END;

IF OBJECT_ID(N'dbo.RetailOrders', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailOrders_VoucherId' AND object_id = OBJECT_ID(N'dbo.RetailOrders'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RetailOrders_VoucherId
        ON dbo.RetailOrders(VoucherId)
        WHERE VoucherId IS NOT NULL;
END;

IF OBJECT_ID(N'dbo.CustomerVouchers', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerVouchers_Voucher_Customer_Used' AND object_id = OBJECT_ID(N'dbo.CustomerVouchers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_CustomerVouchers_Voucher_Customer_Used
        ON dbo.CustomerVouchers(VoucherId, CustomerId, IsUsed);
END;

IF OBJECT_ID(N'dbo.VoucherRedemptions', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VoucherRedemptions_Voucher_AppliedAt' AND object_id = OBJECT_ID(N'dbo.VoucherRedemptions'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_VoucherRedemptions_Voucher_AppliedAt
        ON dbo.VoucherRedemptions(VoucherId, AppliedAt DESC)
        INCLUDE (CustomerId, VehicleId, BranchId, DiscountAmount, RedemptionStatus, FailReasonCode);
END;

IF OBJECT_ID(N'dbo.VoucherRedemptions', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VoucherRedemptions_InvoiceId' AND object_id = OBJECT_ID(N'dbo.VoucherRedemptions'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_VoucherRedemptions_InvoiceId
        ON dbo.VoucherRedemptions(InvoiceId)
        WHERE InvoiceId IS NOT NULL;
END;

IF OBJECT_ID(N'dbo.VoucherRedemptions', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_VoucherRedemptions_IdempotencyKey' AND object_id = OBJECT_ID(N'dbo.VoucherRedemptions'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_VoucherRedemptions_IdempotencyKey
        ON dbo.VoucherRedemptions(IdempotencyKey)
        WHERE IdempotencyKey IS NOT NULL;
END;

IF OBJECT_ID(N'dbo.VoucherApprovalRequests', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VoucherApprovalRequests_Status_SLA' AND object_id = OBJECT_ID(N'dbo.VoucherApprovalRequests'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_VoucherApprovalRequests_Status_SLA
        ON dbo.VoucherApprovalRequests(Status, SLADeadline)
        INCLUDE (RequestType, VoucherId, BranchId, RequestedBy, ApproverId, RiskLevel, ImpactAmount, CreatedAt);
END;

IF OBJECT_ID(N'dbo.VoucherUsageRules', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VoucherUsageRules_VoucherId_RuleType' AND object_id = OBJECT_ID(N'dbo.VoucherUsageRules'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_VoucherUsageRules_VoucherId_RuleType
        ON dbo.VoucherUsageRules(VoucherId, RuleType, IsActive);
END;

IF OBJECT_ID(N'dbo.VoucherAssignments', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VoucherAssignments_Target' AND object_id = OBJECT_ID(N'dbo.VoucherAssignments'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_VoucherAssignments_Target
        ON dbo.VoucherAssignments(AssignmentType, TargetId, IsInclude, VoucherId);
END;

IF OBJECT_ID(N'dbo.VoucherConflictRules', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VoucherConflictRules_VoucherId' AND object_id = OBJECT_ID(N'dbo.VoucherConflictRules'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_VoucherConflictRules_VoucherId
        ON dbo.VoucherConflictRules(VoucherId, IsActive, Priority);
END;

IF OBJECT_ID(N'dbo.VoucherAuditLogs', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VoucherAuditLogs_Entity' AND object_id = OBJECT_ID(N'dbo.VoucherAuditLogs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_VoucherAuditLogs_Entity
        ON dbo.VoucherAuditLogs(EntityName, EntityId, CreatedAt DESC);
END;

PRINT 'Voucher Phase 1 upgrade script completed.';
