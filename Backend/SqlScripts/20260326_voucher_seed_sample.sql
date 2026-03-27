/*
    Voucher sample seed data (idempotent)
    - Target: SQL Server
    - Safe to run multiple times
*/

SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @Now DATETIME2 = GETDATE();
    DECLARE @StaffId BIGINT = (SELECT TOP 1 Id FROM dbo.Staffs ORDER BY Id);
    DECLARE @BranchId BIGINT = (SELECT TOP 1 Id FROM dbo.Branches WHERE ISNULL(IsDeleted, 0) = 0 ORDER BY Id);
    DECLARE @CustomerId BIGINT = (SELECT TOP 1 Id FROM dbo.Customers WHERE ISNULL(IsDeleted, 0) = 0 ORDER BY Id);
    DECLARE @VehicleId BIGINT = (SELECT TOP 1 Id FROM dbo.Vehicles WHERE ISNULL(IsDeleted, 0) = 0 ORDER BY Id);
    DECLARE @PaymentMethodId BIGINT = (SELECT TOP 1 Id FROM dbo.PaymentMethods ORDER BY Id);

    DECLARE @BranchRuleValue NVARCHAR(50) = COALESCE(CONVERT(NVARCHAR(20), @BranchId), N'ALL');
    DECLARE @CampaignIdIsIdentity BIT = CASE WHEN COLUMNPROPERTY(OBJECT_ID(N'dbo.VoucherCampaigns'), 'Id', 'IsIdentity') = 1 THEN 1 ELSE 0 END;
    DECLARE @VoucherIdIsIdentity BIT = CASE WHEN COLUMNPROPERTY(OBJECT_ID(N'dbo.Vouchers'), 'Id', 'IsIdentity') = 1 THEN 1 ELSE 0 END;
    DECLARE @VoucherTypeCheckDef NVARCHAR(MAX);
    DECLARE @TypeFixed VARCHAR(50);
    DECLARE @TypePercent VARCHAR(50);

    DECLARE @AllowedTypes TABLE
    (
        TypeValue VARCHAR(50) PRIMARY KEY
    );

    SET @VoucherTypeCheckDef =
    (
        SELECT TOP 1 cc.definition
        FROM sys.check_constraints cc
        INNER JOIN sys.columns c
            ON c.object_id = cc.parent_object_id
           AND c.column_id = cc.parent_column_id
        WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Vouchers')
          AND c.name = 'Type'
        ORDER BY cc.name
    );

    IF @VoucherTypeCheckDef IS NULL
    BEGIN
        SET @VoucherTypeCheckDef =
        (
            SELECT TOP 1 cc.definition
            FROM sys.check_constraints cc
            WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Vouchers')
              AND cc.name LIKE '%Type%'
            ORDER BY cc.name
        );
    END;

    IF @VoucherTypeCheckDef IS NOT NULL
    BEGIN
        DECLARE @ParsePos INT = 1;
        DECLARE @P1 INT;
        DECLARE @P2 INT;
        DECLARE @Token NVARCHAR(200);

        WHILE 1 = 1
        BEGIN
            SET @P1 = CHARINDEX(N'''', @VoucherTypeCheckDef, @ParsePos);
            IF @P1 = 0 BREAK;

            SET @P2 = CHARINDEX(N'''', @VoucherTypeCheckDef, @P1 + 1);
            IF @P2 = 0 BREAK;

            SET @Token = LTRIM(RTRIM(SUBSTRING(@VoucherTypeCheckDef, @P1 + 1, @P2 - @P1 - 1)));
            IF @Token <> N''
            BEGIN
                INSERT INTO @AllowedTypes(TypeValue)
                SELECT CAST(@Token AS VARCHAR(50))
                WHERE NOT EXISTS (SELECT 1 FROM @AllowedTypes WHERE TypeValue = @Token);
            END;

            SET @ParsePos = @P2 + 1;
        END;
    END;

    SELECT TOP 1 @TypePercent = TypeValue
    FROM @AllowedTypes
    WHERE UPPER(TypeValue) LIKE '%PERCENT%'
       OR UPPER(TypeValue) LIKE '%PCT%'
       OR UPPER(TypeValue) LIKE '%RATE%'
       OR UPPER(TypeValue) LIKE '%PHANTRAM%';

    SELECT TOP 1 @TypeFixed = TypeValue
    FROM @AllowedTypes
    WHERE UPPER(TypeValue) NOT LIKE '%PERCENT%'
      AND UPPER(TypeValue) NOT LIKE '%PCT%'
      AND UPPER(TypeValue) NOT LIKE '%RATE%'
      AND UPPER(TypeValue) NOT LIKE '%PHANTRAM%';

    IF @TypeFixed IS NULL
    BEGIN
        SELECT TOP 1 @TypeFixed = TypeValue
        FROM @AllowedTypes
        ORDER BY TypeValue;
    END;

    IF @TypeFixed IS NULL
        SET @TypeFixed = 'FIXED';

    IF @TypePercent IS NULL
        SET @TypePercent = @TypeFixed;

    PRINT 'Voucher type seed - fixed: ' + ISNULL(@TypeFixed, 'NULL') + ', percent: ' + ISNULL(@TypePercent, 'NULL');

    IF @CampaignIdIsIdentity = 1
        SET IDENTITY_INSERT dbo.VoucherCampaigns ON;

    /* =========================================================
       1) Campaigns
       ========================================================= */
    DECLARE @CampaignTetId BIGINT = (
        SELECT TOP 1 Id FROM dbo.VoucherCampaigns WHERE CampaignCode = 'CAMP-TET-2026'
    );

    IF @CampaignTetId IS NULL
    BEGIN
        SET @CampaignTetId = ISNULL((SELECT MAX(Id) FROM dbo.VoucherCampaigns), 0) + 1;

        INSERT INTO dbo.VoucherCampaigns
        (
            Id, CampaignCode, Name, Objective, Description,
            StartDate, EndDate, BudgetLimit, BudgetUsed,
            Status, OwnerStaffId, IsActive,
            CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted
        )
        VALUES
        (
            @CampaignTetId,
            'CAMP-TET-2026',
            N'Chiến dịch Tết 2026',
            N'Tăng lượt quay lại đầu năm',
            N'Voucher áp dụng dịp Tết cho dịch vụ bảo dưỡng.',
            DATEFROMPARTS(2026, 1, 1),
            DATEFROMPARTS(2026, 2, 28),
            50000000,
            0,
            'ACTIVE',
            @StaffId,
            1,
            @Now,
            @StaffId,
            @Now,
            @StaffId,
            0
        );
    END;

    DECLARE @CampaignCskhId BIGINT = (
        SELECT TOP 1 Id FROM dbo.VoucherCampaigns WHERE CampaignCode = 'CAMP-CSKH-2026'
    );

    IF @CampaignCskhId IS NULL
    BEGIN
        SET @CampaignCskhId = ISNULL((SELECT MAX(Id) FROM dbo.VoucherCampaigns), 0) + 1;

        INSERT INTO dbo.VoucherCampaigns
        (
            Id, CampaignCode, Name, Objective, Description,
            StartDate, EndDate, BudgetLimit, BudgetUsed,
            Status, OwnerStaffId, IsActive,
            CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted
        )
        VALUES
        (
            @CampaignCskhId,
            'CAMP-CSKH-2026',
            N'Chiến dịch CSKH 2026',
            N'Xử lý khiếu nại và giữ chân khách hàng',
            N'Voucher bù dịch vụ cho các case chăm sóc khách hàng.',
            DATEFROMPARTS(2026, 1, 1),
            DATEFROMPARTS(2026, 12, 31),
            30000000,
            0,
            'ACTIVE',
            @StaffId,
            1,
            @Now,
            @StaffId,
            @Now,
            @StaffId,
            0
        );
    END;

    IF @CampaignIdIsIdentity = 1
        SET IDENTITY_INSERT dbo.VoucherCampaigns OFF;

    /* =========================================================
       2) Vouchers
       ========================================================= */
    IF @VoucherIdIsIdentity = 1
        SET IDENTITY_INSERT dbo.Vouchers ON;

    DECLARE @VoucherTetId BIGINT = (SELECT TOP 1 Id FROM dbo.Vouchers WHERE Code = 'VC-TET-100K');
    IF @VoucherTetId IS NULL
    BEGIN
        SET @VoucherTetId = ISNULL((SELECT MAX(Id) FROM dbo.Vouchers), 0) + 1;

        INSERT INTO dbo.Vouchers
        (
            Id, Code, VoucherName, Description,
            Type, Value, MinOrderValue, MaxDiscountAmount,
            StartDate, EndDate, ValidFrom, ValidTo, ValidDaysOfWeek,
            UsageLimit, UsedCount, IssuedCount, RedeemedCount,
            MaxUsesPerCustomer, MaxUsesPerVehicle,
            BudgetLimit, BudgetUsed,
            IsStackable, FirstUseOnly, ExcludeDiscountedItems,
            AllowedChannels, AllowedPaymentMethods,
            Timezone, ApprovalRequired, ApprovalThresholdAmount,
            CampaignId, Status, IsActive,
            CreatedAt, CreatedBy, UpdatedAt, UpdatedBy,
            IsDeleted
        )
        VALUES
        (
            @VoucherTetId,
            'VC-TET-100K',
            N'Tết 100K',
            N'Giảm cố định 100.000 VND cho hóa đơn từ 800.000.',
            @TypeFixed, 100000, 800000, 100000,
            DATEFROMPARTS(2026, 1, 1), DATEFROMPARTS(2026, 2, 28),
            '08:00', '21:00', '1,2,3,4,5,6,7',
            2000, 0, 600, 0,
            2, 1,
            20000000, 0,
            0, 0, 1,
            N'ONLINE,APP,WALK_IN', N'CASH,CARD,TRANSFER',
            'Asia/Ho_Chi_Minh', 0, NULL,
            @CampaignTetId, 'ACTIVE', 1,
            @Now, @StaffId, @Now, @StaffId,
            0
        );
    END;

    DECLARE @VoucherLoyalId BIGINT = (SELECT TOP 1 Id FROM dbo.Vouchers WHERE Code = 'VC-LOYAL-10P');
    IF @VoucherLoyalId IS NULL
    BEGIN
        SET @VoucherLoyalId = ISNULL((SELECT MAX(Id) FROM dbo.Vouchers), 0) + 1;

        INSERT INTO dbo.Vouchers
        (
            Id, Code, VoucherName, Description,
            Type, Value, MinOrderValue, MaxDiscountAmount,
            StartDate, EndDate, ValidFrom, ValidTo, ValidDaysOfWeek,
            UsageLimit, UsedCount, IssuedCount, RedeemedCount,
            MaxUsesPerCustomer, MaxUsesPerVehicle,
            BudgetLimit, BudgetUsed,
            IsStackable, FirstUseOnly, ExcludeDiscountedItems,
            AllowedChannels, AllowedPaymentMethods,
            Timezone, ApprovalRequired, ApprovalThresholdAmount,
            CampaignId, Status, IsActive,
            CreatedAt, CreatedBy, UpdatedAt, UpdatedBy,
            IsDeleted
        )
        VALUES
        (
            @VoucherLoyalId,
            'VC-LOYAL-10P',
            N'Thân thiết 10%',
            N'Giảm 10% tối đa 300.000 cho khách quay lại.',
            @TypePercent, 10, 500000, 300000,
            DATEFROMPARTS(2026, 1, 1), DATEFROMPARTS(2026, 12, 31),
            '08:00', '20:00', '1,2,3,4,5,6,7',
            5000, 0, 1200, 0,
            3, 2,
            45000000, 0,
            1, 0, 0,
            N'ONLINE,APP,WALK_IN,HOTLINE', N'CASH,CARD,TRANSFER,EWALLET',
            'Asia/Ho_Chi_Minh', 0, NULL,
            @CampaignTetId, 'ACTIVE', 1,
            @Now, @StaffId, @Now, @StaffId,
            0
        );
    END;

    DECLARE @VoucherCompId BIGINT = (SELECT TOP 1 Id FROM dbo.Vouchers WHERE Code = 'VC-CSKH-200K');
    IF @VoucherCompId IS NULL
    BEGIN
        SET @VoucherCompId = ISNULL((SELECT MAX(Id) FROM dbo.Vouchers), 0) + 1;

        INSERT INTO dbo.Vouchers
        (
            Id, Code, VoucherName, Description,
            Type, Value, MinOrderValue, MaxDiscountAmount,
            StartDate, EndDate, ValidFrom, ValidTo, ValidDaysOfWeek,
            UsageLimit, UsedCount, IssuedCount, RedeemedCount,
            MaxUsesPerCustomer, MaxUsesPerVehicle,
            BudgetLimit, BudgetUsed,
            IsStackable, FirstUseOnly, ExcludeDiscountedItems,
            AllowedChannels, AllowedPaymentMethods,
            Timezone, ApprovalRequired, ApprovalThresholdAmount,
            CampaignId, Status, IsActive,
            CreatedAt, CreatedBy, UpdatedAt, UpdatedBy,
            IsDeleted
        )
        VALUES
        (
            @VoucherCompId,
            'VC-CSKH-200K',
            N'Bù dịch vụ 200K',
            N'Voucher bù dịch vụ cho case khiếu nại, cần phê duyệt.',
            @TypeFixed, 200000, 0, 200000,
            DATEFROMPARTS(2026, 1, 1), DATEFROMPARTS(2026, 12, 31),
            '00:00', '23:59', '1,2,3,4,5,6,7',
            300, 0, 120, 0,
            1, 1,
            15000000, 0,
            0, 0, 0,
            N'WALK_IN,HOTLINE', N'CASH,CARD,TRANSFER',
            'Asia/Ho_Chi_Minh', 1, 150000,
            @CampaignCskhId, 'ACTIVE', 1,
            @Now, @StaffId, @Now, @StaffId,
            0
        );
    END;

    IF @VoucherIdIsIdentity = 1
        SET IDENTITY_INSERT dbo.Vouchers OFF;

    /* =========================================================
       3) Rules + assignments + conflict rules
       ========================================================= */
    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherUsageRules WHERE VoucherId = @VoucherTetId AND RuleType = 'MIN_INVOICE' AND Operator = 'GTE' AND RuleValue = N'800000')
        INSERT INTO dbo.VoucherUsageRules (VoucherId, RuleType, Operator, RuleValue, IsExclude, HardBlock, Priority, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (@VoucherTetId, 'MIN_INVOICE', 'GTE', N'800000', 0, 1, 10, 1, @Now, @StaffId, @Now, @StaffId);

    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherUsageRules WHERE VoucherId = @VoucherTetId AND RuleType = 'BOOKING_CHANNEL' AND Operator = 'IN' AND RuleValue = N'ONLINE,APP,WALK_IN')
        INSERT INTO dbo.VoucherUsageRules (VoucherId, RuleType, Operator, RuleValue, IsExclude, HardBlock, Priority, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (@VoucherTetId, 'BOOKING_CHANNEL', 'IN', N'ONLINE,APP,WALK_IN', 0, 1, 20, 1, @Now, @StaffId, @Now, @StaffId);

    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherUsageRules WHERE VoucherId = @VoucherLoyalId AND RuleType = 'MAX_USE_PER_CUSTOMER' AND Operator = 'LTE' AND RuleValue = N'3')
        INSERT INTO dbo.VoucherUsageRules (VoucherId, RuleType, Operator, RuleValue, IsExclude, HardBlock, Priority, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (@VoucherLoyalId, 'MAX_USE_PER_CUSTOMER', 'LTE', N'3', 0, 1, 20, 1, @Now, @StaffId, @Now, @StaffId);

    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherUsageRules WHERE VoucherId = @VoucherCompId AND RuleType = 'PAYMENT_METHOD' AND Operator = 'IN' AND RuleValue = N'CASH,CARD,TRANSFER')
        INSERT INTO dbo.VoucherUsageRules (VoucherId, RuleType, Operator, RuleValue, IsExclude, HardBlock, Priority, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (@VoucherCompId, 'PAYMENT_METHOD', 'IN', N'CASH,CARD,TRANSFER', 0, 1, 10, 1, @Now, @StaffId, @Now, @StaffId);

    IF @BranchId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.VoucherAssignments WHERE VoucherId = @VoucherTetId AND AssignmentType = 'BRANCH' AND TargetId = @BranchId AND IsInclude = 1)
            INSERT INTO dbo.VoucherAssignments (VoucherId, AssignmentType, TargetId, IsInclude, EffectiveFrom, EffectiveTo, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
            VALUES (@VoucherTetId, 'BRANCH', @BranchId, 1, DATEFROMPARTS(2026, 1, 1), DATEFROMPARTS(2026, 2, 28), @Now, @StaffId, @Now, @StaffId);

        IF NOT EXISTS (SELECT 1 FROM dbo.VoucherAssignments WHERE VoucherId = @VoucherLoyalId AND AssignmentType = 'BRANCH' AND TargetId = @BranchId AND IsInclude = 1)
            INSERT INTO dbo.VoucherAssignments (VoucherId, AssignmentType, TargetId, IsInclude, EffectiveFrom, EffectiveTo, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
            VALUES (@VoucherLoyalId, 'BRANCH', @BranchId, 1, DATEFROMPARTS(2026, 1, 1), DATEFROMPARTS(2026, 12, 31), @Now, @StaffId, @Now, @StaffId);
    END;

    IF @CustomerId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.VoucherAssignments WHERE VoucherId = @VoucherCompId AND AssignmentType = 'CUSTOMER' AND TargetId = @CustomerId AND IsInclude = 1)
            INSERT INTO dbo.VoucherAssignments (VoucherId, AssignmentType, TargetId, IsInclude, EffectiveFrom, EffectiveTo, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
            VALUES (@VoucherCompId, 'CUSTOMER', @CustomerId, 1, DATEFROMPARTS(2026, 1, 1), DATEFROMPARTS(2026, 12, 31), @Now, @StaffId, @Now, @StaffId);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherConflictRules WHERE VoucherId = @VoucherTetId AND ConflictType = 'STACKING' AND ConflictTarget = N'ANY')
        INSERT INTO dbo.VoucherConflictRules (VoucherId, ConflictType, ConflictTarget, ActionType, MessageCode, MessageText, Priority, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (@VoucherTetId, 'STACKING', N'ANY', 'BLOCK', 'NOT_STACKABLE', N'Voucher Tết không được cộng dồn với ưu đãi khác.', 10, 1, @Now, @StaffId, @Now, @StaffId);

    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherConflictRules WHERE VoucherId = @VoucherCompId AND ConflictType = 'MANUAL_DISCOUNT' AND ConflictTarget = N'ANY')
        INSERT INTO dbo.VoucherConflictRules (VoucherId, ConflictType, ConflictTarget, ActionType, MessageCode, MessageText, Priority, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (@VoucherCompId, 'MANUAL_DISCOUNT', N'ANY', 'REQUIRE_APPROVAL', 'REQ_APPROVAL', N'Kết hợp voucher bù dịch vụ với giảm tay cần phê duyệt.', 5, 1, @Now, @StaffId, @Now, @StaffId);

    /* =========================================================
       4) Approval requests + redemptions
       ========================================================= */
    IF @StaffId IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM dbo.VoucherApprovalRequests
            WHERE RequestType = 'EXPIRED_OVERRIDE'
              AND VoucherId = @VoucherTetId
              AND Status = 'PENDING'
              AND Reason = N'Cho phép dùng ngoại lệ cho khách VIP đến trễ lịch hẹn'
        )
        BEGIN
            INSERT INTO dbo.VoucherApprovalRequests
            (
                RequestType, VoucherId, BranchId, RequestedBy, ApproverId,
                Status, RiskLevel, Reason, RequestPayload, ImpactAmount,
                SLADeadline, CreatedAt, UpdatedAt
            )
            VALUES
            (
                'EXPIRED_OVERRIDE', @VoucherTetId, @BranchId, @StaffId, NULL,
                'PENDING', 'HIGH', N'Cho phép dùng ngoại lệ cho khách VIP đến trễ lịch hẹn',
                N'{"invoiceAmount":1200000,"expectedDiscount":100000}', 100000,
                DATEADD(HOUR, 4, @Now), @Now, @Now
            );
        END;
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherRedemptions WHERE IdempotencyKey = 'seed-vc-tet-100k-01')
    BEGIN
        INSERT INTO dbo.VoucherRedemptions
        (
            VoucherId, VoucherCode, CustomerId, VehicleId, BranchId,
            PaymentMethodId, ValidationStatus, RedemptionStatus,
            EligibleAmount, DiscountAmount, BudgetImpact,
            AppliedBy, ApprovedBy, IdempotencyKey,
            AppliedAt, RedeemedAt, CreatedAt
        )
        VALUES
        (
            @VoucherTetId, 'VC-TET-100K', @CustomerId, @VehicleId, @BranchId,
            @PaymentMethodId, 'VALIDATED', 'REDEEMED',
            1200000, 100000, 100000,
            @StaffId, @StaffId, 'seed-vc-tet-100k-01',
            DATEADD(DAY, -6, @Now), DATEADD(DAY, -6, @Now), DATEADD(DAY, -6, @Now)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherRedemptions WHERE IdempotencyKey = 'seed-vc-loyal-10p-01')
    BEGIN
        INSERT INTO dbo.VoucherRedemptions
        (
            VoucherId, VoucherCode, CustomerId, VehicleId, BranchId,
            PaymentMethodId, ValidationStatus, RedemptionStatus,
            EligibleAmount, DiscountAmount, BudgetImpact,
            AppliedBy, ApprovedBy, IdempotencyKey,
            AppliedAt, RedeemedAt, CreatedAt
        )
        VALUES
        (
            @VoucherLoyalId, 'VC-LOYAL-10P', @CustomerId, @VehicleId, @BranchId,
            @PaymentMethodId, 'VALIDATED', 'REDEEMED',
            2500000, 250000, 250000,
            @StaffId, @StaffId, 'seed-vc-loyal-10p-01',
            DATEADD(DAY, -4, @Now), DATEADD(DAY, -4, @Now), DATEADD(DAY, -4, @Now)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherRedemptions WHERE IdempotencyKey = 'seed-vc-cskh-200k-fail-01')
    BEGIN
        INSERT INTO dbo.VoucherRedemptions
        (
            VoucherId, VoucherCode, CustomerId, VehicleId, BranchId,
            PaymentMethodId, ValidationStatus, RedemptionStatus,
            FailReasonCode, EligibleAmount, DiscountAmount, BudgetImpact,
            AppliedBy, IdempotencyKey,
            AppliedAt, CreatedAt
        )
        VALUES
        (
            @VoucherCompId, 'VC-CSKH-200K', @CustomerId, @VehicleId, @BranchId,
            @PaymentMethodId, 'FAILED', 'FAILED',
            'NOT_APPROVED', 700000, 0, 0,
            @StaffId, 'seed-vc-cskh-200k-fail-01',
            DATEADD(DAY, -2, @Now), DATEADD(DAY, -2, @Now)
        );
    END;
    /* =========================================================
       5) Code batches + audit logs
       ========================================================= */
    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherCodeBatches WHERE BatchCode = 'BATCH-TET-2026-001')
    BEGIN
        INSERT INTO dbo.VoucherCodeBatches
        (
            VoucherId, BatchCode, SourceType,
            TotalCount, SuccessCount, DuplicateCount, InvalidCount,
            FileName, Status, CreatedBy, CreatedAt
        )
        VALUES
        (
            @VoucherTetId, 'BATCH-TET-2026-001', 'GENERATED',
            600, 600, 0, 0,
            N'seed_generated_tet_2026.csv', 'COMPLETED', @StaffId, @Now
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherCodeBatches WHERE BatchCode = 'BATCH-LOYAL-2026-001')
    BEGIN
        INSERT INTO dbo.VoucherCodeBatches
        (
            VoucherId, BatchCode, SourceType,
            TotalCount, SuccessCount, DuplicateCount, InvalidCount,
            FileName, Status, CreatedBy, CreatedAt
        )
        VALUES
        (
            @VoucherLoyalId, 'BATCH-LOYAL-2026-001', 'IMPORTED',
            1200, 1190, 5, 5,
            N'seed_import_loyal_2026.csv', 'COMPLETED', @StaffId, @Now
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherAuditLogs WHERE CorrelationId = 'seed-create-vc-tet-100k')
    BEGIN
        INSERT INTO dbo.VoucherAuditLogs
        (
            VoucherId, EntityName, EntityId, Action,
            ReasonCode, BeforeData, AfterData,
            PerformedBy, BranchId, CorrelationId, CreatedAt
        )
        VALUES
        (
            @VoucherTetId, 'Voucher', @VoucherTetId, 'CREATE',
            'SEED', NULL,
            N'{"code":"VC-TET-100K","status":"ACTIVE"}',
            @StaffId, @BranchId, 'seed-create-vc-tet-100k', @Now
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.VoucherAuditLogs WHERE CorrelationId = 'seed-create-vc-loyal-10p')
    BEGIN
        INSERT INTO dbo.VoucherAuditLogs
        (
            VoucherId, EntityName, EntityId, Action,
            ReasonCode, BeforeData, AfterData,
            PerformedBy, BranchId, CorrelationId, CreatedAt
        )
        VALUES
        (
            @VoucherLoyalId, 'Voucher', @VoucherLoyalId, 'CREATE',
            'SEED', NULL,
            N'{"code":"VC-LOYAL-10P","status":"ACTIVE"}',
            @StaffId, @BranchId, 'seed-create-vc-loyal-10p', @Now
        );
    END;

    /* =========================================================
       6) Re-calc counters
       ========================================================= */
    ;WITH RedeemAgg AS
    (
        SELECT
            r.VoucherId,
            SUM(CASE WHEN r.RedemptionStatus = 'REDEEMED' THEN 1 ELSE 0 END) AS RedeemCount,
            SUM(CASE WHEN r.RedemptionStatus = 'REDEEMED' THEN ISNULL(r.BudgetImpact, 0) ELSE 0 END) AS BudgetUsed
        FROM dbo.VoucherRedemptions r
        WHERE r.VoucherId IN (@VoucherTetId, @VoucherLoyalId, @VoucherCompId)
        GROUP BY r.VoucherId
    )
    UPDATE v
        SET v.UsedCount = ISNULL(a.RedeemCount, 0),
            v.RedeemedCount = ISNULL(a.RedeemCount, 0),
            v.BudgetUsed = ISNULL(a.BudgetUsed, 0),
            v.IssuedCount = CASE
                                WHEN ISNULL(v.IssuedCount, 0) < ISNULL(a.RedeemCount, 0)
                                    THEN ISNULL(a.RedeemCount, 0) + 10
                                ELSE ISNULL(v.IssuedCount, 0)
                            END,
            v.UpdatedAt = @Now,
            v.UpdatedBy = @StaffId
    FROM dbo.Vouchers v
    LEFT JOIN RedeemAgg a ON a.VoucherId = v.Id
    WHERE v.Id IN (@VoucherTetId, @VoucherLoyalId, @VoucherCompId);

    UPDATE c
        SET c.BudgetUsed = ISNULL(x.BudgetUsed, 0),
            c.UpdatedAt = @Now,
            c.UpdatedBy = @StaffId
    FROM dbo.VoucherCampaigns c
    OUTER APPLY
    (
        SELECT SUM(ISNULL(v.BudgetUsed, 0)) AS BudgetUsed
        FROM dbo.Vouchers v
        WHERE v.CampaignId = c.Id
    ) x
    WHERE c.Id IN (@CampaignTetId, @CampaignCskhId);

    COMMIT TRAN;

    PRINT 'Seed voucher sample data completed.';
    PRINT 'Campaign IDs: ' + CAST(@CampaignTetId AS VARCHAR(20)) + ', ' + CAST(@CampaignCskhId AS VARCHAR(20));
    PRINT 'Voucher IDs : ' + CAST(@VoucherTetId AS VARCHAR(20)) + ', ' + CAST(@VoucherLoyalId AS VARCHAR(20)) + ', ' + CAST(@VoucherCompId AS VARCHAR(20));
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;

    DECLARE @Err NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @Line INT = ERROR_LINE();
    DECLARE @Num INT = ERROR_NUMBER();

    BEGIN TRY
        IF COLUMNPROPERTY(OBJECT_ID(N'dbo.Vouchers'), 'Id', 'IsIdentity') = 1
            SET IDENTITY_INSERT dbo.Vouchers OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    BEGIN TRY
        IF COLUMNPROPERTY(OBJECT_ID(N'dbo.VoucherCampaigns'), 'Id', 'IsIdentity') = 1
            SET IDENTITY_INSERT dbo.VoucherCampaigns OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    RAISERROR(N'Seed voucher thất bại (line %d, error %d): %s', 16, 1, @Line, @Num, @Err);
END CATCH;
