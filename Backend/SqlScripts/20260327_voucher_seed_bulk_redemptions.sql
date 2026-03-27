/*
    Voucher bulk seed data (idempotent)
    - Tạo dữ liệu mẫu lớn cho dashboard/report
    - Chạy sau khi đã có voucher seed cơ bản
*/

SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @Now DATETIME2 = GETDATE();
    DECLARE @BatchTag VARCHAR(30) = '20260327A';
    DECLARE @Rows INT = 90;
    DECLARE @InsertedRedemptions INT = 0;
    DECLARE @InsertedApprovals INT = 0;

    DECLARE @StaffFallback BIGINT = (SELECT TOP 1 Id FROM dbo.Staffs ORDER BY Id);
    IF @StaffFallback IS NULL
        RAISERROR(N'Không tìm thấy dữ liệu Staffs để seed voucher.', 16, 1);

    IF OBJECT_ID('tempdb..#VoucherPool') IS NOT NULL DROP TABLE #VoucherPool;
    CREATE TABLE #VoucherPool
    (
        Rn INT NOT NULL PRIMARY KEY,
        VoucherId BIGINT NOT NULL,
        VoucherCode VARCHAR(50) NOT NULL,
        VoucherType VARCHAR(50) NOT NULL,
        VoucherValue DECIMAL(18,2) NOT NULL,
        MaxDiscountAmount DECIMAL(18,2) NULL,
        CampaignId BIGINT NULL
    );

    INSERT INTO #VoucherPool(Rn, VoucherId, VoucherCode, VoucherType, VoucherValue, MaxDiscountAmount, CampaignId)
    SELECT
        ROW_NUMBER() OVER (ORDER BY v.Id) AS Rn,
        v.Id,
        v.Code,
        v.Type,
        v.Value,
        v.MaxDiscountAmount,
        v.CampaignId
    FROM dbo.Vouchers v
    WHERE v.Code IN ('VC-TET-100K', 'VC-LOYAL-10P', 'VC-CSKH-200K')
      AND ISNULL(v.IsDeleted, 0) = 0;

    DECLARE @VoucherCount INT = (SELECT COUNT(1) FROM #VoucherPool);
    IF @VoucherCount = 0
        RAISERROR(N'Không tìm thấy voucher seed nền (VC-TET-100K / VC-LOYAL-10P / VC-CSKH-200K).', 16, 1);

    IF OBJECT_ID('tempdb..#CustomerPool') IS NOT NULL DROP TABLE #CustomerPool;
    CREATE TABLE #CustomerPool (Rn INT NOT NULL PRIMARY KEY, Id BIGINT NOT NULL);
    INSERT INTO #CustomerPool(Rn, Id)
    SELECT ROW_NUMBER() OVER (ORDER BY c.Id), c.Id
    FROM dbo.Customers c
    WHERE ISNULL(c.IsDeleted, 0) = 0;

    IF OBJECT_ID('tempdb..#VehiclePool') IS NOT NULL DROP TABLE #VehiclePool;
    CREATE TABLE #VehiclePool (Rn INT NOT NULL PRIMARY KEY, Id BIGINT NOT NULL);
    INSERT INTO #VehiclePool(Rn, Id)
    SELECT ROW_NUMBER() OVER (ORDER BY v.Id), v.Id
    FROM dbo.Vehicles v
    WHERE ISNULL(v.IsDeleted, 0) = 0;

    IF OBJECT_ID('tempdb..#BranchPool') IS NOT NULL DROP TABLE #BranchPool;
    CREATE TABLE #BranchPool (Rn INT NOT NULL PRIMARY KEY, Id BIGINT NOT NULL);
    INSERT INTO #BranchPool(Rn, Id)
    SELECT ROW_NUMBER() OVER (ORDER BY b.Id), b.Id
    FROM dbo.Branches b
    WHERE ISNULL(b.IsDeleted, 0) = 0;

    IF OBJECT_ID('tempdb..#PaymentPool') IS NOT NULL DROP TABLE #PaymentPool;
    CREATE TABLE #PaymentPool (Rn INT NOT NULL PRIMARY KEY, Id BIGINT NOT NULL);
    INSERT INTO #PaymentPool(Rn, Id)
    SELECT ROW_NUMBER() OVER (ORDER BY p.Id), p.Id
    FROM dbo.PaymentMethods p;

    IF OBJECT_ID('tempdb..#StaffPool') IS NOT NULL DROP TABLE #StaffPool;
    CREATE TABLE #StaffPool (Rn INT NOT NULL PRIMARY KEY, Id BIGINT NOT NULL);
    INSERT INTO #StaffPool(Rn, Id)
    SELECT ROW_NUMBER() OVER (ORDER BY s.Id), s.Id
    FROM dbo.Staffs s
    WHERE ISNULL(s.IsDeleted, 0) = 0;

    DECLARE @CustomerCount INT = (SELECT COUNT(1) FROM #CustomerPool);
    DECLARE @VehicleCount INT = (SELECT COUNT(1) FROM #VehiclePool);
    DECLARE @BranchCount INT = (SELECT COUNT(1) FROM #BranchPool);
    DECLARE @PaymentCount INT = (SELECT COUNT(1) FROM #PaymentPool);
    DECLARE @StaffCount INT = (SELECT COUNT(1) FROM #StaffPool);

    ;WITH N AS
    (
        SELECT TOP (@Rows) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
        FROM sys.all_objects
    ),
    Base AS
    (
        SELECT
            n.N,
            vp.VoucherId,
            vp.VoucherCode,
            vp.VoucherType,
            vp.VoucherValue,
            vp.MaxDiscountAmount,
            CONCAT('bulk-vr-', @BatchTag, '-', RIGHT('0000' + CAST(n.N AS VARCHAR(10)), 4)) AS IdempotencyKey,
            DATEADD(HOUR, -n.N * 6, @Now) AS AppliedAt,
            CAST(400000 + ((n.N * 135791) % 2600000) AS DECIMAL(18,2)) AS EligibleAmount,
            CASE
                WHEN (n.N % 10) = 0 THEN 'FAILED'
                WHEN (n.N % 10) IN (1,2) THEN 'REVERSED'
                ELSE 'REDEEMED'
            END AS RedemptionStatus,
            CASE
                WHEN (n.N % 10) = 0 THEN 'FAILED'
                ELSE 'VALIDATED'
            END AS ValidationStatus,
            CASE
                WHEN (n.N % 10) = 0 THEN CASE WHEN n.N % 2 = 0 THEN 'MIN_INVOICE' ELSE 'OUT_OF_SCOPE' END
                ELSE NULL
            END AS FailReasonCode
        FROM N n
        INNER JOIN #VoucherPool vp
            ON vp.Rn = ((n.N - 1) % @VoucherCount) + 1
    ),
    Calc AS
    (
        SELECT
            b.*,
            CASE
                WHEN UPPER(b.VoucherType) LIKE '%PERCENT%'
                  OR UPPER(b.VoucherType) LIKE '%PCT%'
                  OR UPPER(b.VoucherType) LIKE '%RATE%'
                  OR UPPER(b.VoucherType) LIKE '%PHANTRAM%'
                THEN CAST(b.EligibleAmount * (b.VoucherValue / 100.0) AS DECIMAL(18,2))
                ELSE CAST(b.VoucherValue AS DECIMAL(18,2))
            END AS RawDiscount
        FROM Base b
    ),
    FinalRows AS
    (
        SELECT
            c.*,
            CAST(
                CASE
                    WHEN c.RedemptionStatus = 'FAILED' THEN 0
                    WHEN c.MaxDiscountAmount IS NULL THEN IIF(c.RawDiscount > c.EligibleAmount, c.EligibleAmount, c.RawDiscount)
                    ELSE
                        IIF(
                            IIF(c.RawDiscount > c.EligibleAmount, c.EligibleAmount, c.RawDiscount) > c.MaxDiscountAmount,
                            c.MaxDiscountAmount,
                            IIF(c.RawDiscount > c.EligibleAmount, c.EligibleAmount, c.RawDiscount)
                        )
                END
            AS DECIMAL(18,2)) AS DiscountAmount,
            DATEADD(MINUTE, 5, c.AppliedAt) AS RedeemedAt,
            CASE WHEN c.RedemptionStatus = 'REVERSED' THEN DATEADD(HOUR, 8, c.AppliedAt) ELSE NULL END AS ReversedAt,
            CASE WHEN c.RedemptionStatus = 'REVERSED' THEN N'Khách đổi gói dịch vụ sau khi áp voucher' ELSE NULL END AS ReverseReason
        FROM Calc c
    )
    INSERT INTO dbo.VoucherRedemptions
    (
        VoucherId, VoucherCode,
        CustomerId, VehicleId, BranchId,
        PaymentMethodId,
        ValidationStatus, RedemptionStatus, FailReasonCode,
        EligibleAmount, DiscountAmount, BudgetImpact,
        AppliedBy, ApprovedBy,
        IdempotencyKey,
        AppliedAt, RedeemedAt, ReversedAt, ReverseReason,
        CreatedAt
    )
    SELECT
        f.VoucherId,
        f.VoucherCode,
        cp.Id,
        vp.Id,
        bp.Id,
        pp.Id,
        f.ValidationStatus,
        f.RedemptionStatus,
        f.FailReasonCode,
        f.EligibleAmount,
        f.DiscountAmount,
        CASE WHEN f.RedemptionStatus = 'REDEEMED' THEN f.DiscountAmount ELSE 0 END AS BudgetImpact,
        sp1.Id,
        sp2.Id,
        f.IdempotencyKey,
        f.AppliedAt,
        CASE WHEN f.RedemptionStatus IN ('REDEEMED', 'REVERSED') THEN f.RedeemedAt ELSE NULL END,
        f.ReversedAt,
        f.ReverseReason,
        f.AppliedAt
    FROM FinalRows f
    LEFT JOIN #CustomerPool cp ON cp.Rn = ((f.N - 1) % NULLIF(@CustomerCount, 0)) + 1
    LEFT JOIN #VehiclePool vp ON vp.Rn = ((f.N - 1) % NULLIF(@VehicleCount, 0)) + 1
    LEFT JOIN #BranchPool bp ON bp.Rn = ((f.N - 1) % NULLIF(@BranchCount, 0)) + 1
    LEFT JOIN #PaymentPool pp ON pp.Rn = ((f.N - 1) % NULLIF(@PaymentCount, 0)) + 1
    LEFT JOIN #StaffPool sp1 ON sp1.Rn = ((f.N - 1) % NULLIF(@StaffCount, 0)) + 1
    LEFT JOIN #StaffPool sp2 ON sp2.Rn = ((f.N + 1) % NULLIF(@StaffCount, 0)) + 1
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.VoucherRedemptions r
        WHERE r.IdempotencyKey = f.IdempotencyKey
    );

    SET @InsertedRedemptions = @@ROWCOUNT;

    ;WITH A AS
    (
        SELECT
            r.VoucherId,
            SUM(CASE WHEN r.RedemptionStatus = 'REDEEMED' THEN 1 ELSE 0 END) AS RedeemCount,
            SUM(CASE WHEN r.RedemptionStatus = 'REDEEMED' THEN ISNULL(r.BudgetImpact, 0) ELSE 0 END) AS BudgetUsed
        FROM dbo.VoucherRedemptions r
        WHERE r.VoucherId IN (SELECT VoucherId FROM #VoucherPool)
        GROUP BY r.VoucherId
    )
    UPDATE v
        SET v.UsedCount = ISNULL(a.RedeemCount, 0),
            v.RedeemedCount = ISNULL(a.RedeemCount, 0),
            v.BudgetUsed = ISNULL(a.BudgetUsed, 0),
            v.IssuedCount = CASE WHEN ISNULL(v.IssuedCount, 0) < ISNULL(a.RedeemCount, 0) THEN ISNULL(a.RedeemCount, 0) + 30 ELSE ISNULL(v.IssuedCount, 0) END,
            v.UpdatedAt = @Now,
            v.UpdatedBy = @StaffFallback
    FROM dbo.Vouchers v
    LEFT JOIN A a ON a.VoucherId = v.Id
    WHERE v.Id IN (SELECT VoucherId FROM #VoucherPool);

    UPDATE c
        SET c.BudgetUsed = ISNULL(x.TotalBudgetUsed, 0),
            c.UpdatedAt = @Now,
            c.UpdatedBy = @StaffFallback
    FROM dbo.VoucherCampaigns c
    OUTER APPLY
    (
        SELECT SUM(ISNULL(v.BudgetUsed, 0)) AS TotalBudgetUsed
        FROM dbo.Vouchers v
        WHERE v.CampaignId = c.Id
    ) x
    WHERE c.Id IN (SELECT DISTINCT CampaignId FROM #VoucherPool WHERE CampaignId IS NOT NULL);

    ;WITH N AS
    (
        SELECT TOP (18) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
        FROM sys.all_objects
    ),
    R AS
    (
        SELECT
            n.N,
            vp.VoucherId,
            CASE
                WHEN n.N % 4 = 0 THEN 'EXCEED_QUOTA'
                WHEN n.N % 4 = 1 THEN 'DISCOUNT_OVER_LIMIT'
                WHEN n.N % 4 = 2 THEN 'COMBINE_WITH_MANUAL_DISCOUNT'
                ELSE 'APPLY_OUTSIDE_RULE'
            END AS RequestType,
            CASE
                WHEN n.N % 6 IN (0,1) THEN 'PENDING'
                WHEN n.N % 6 IN (2,3,4) THEN 'APPROVED'
                ELSE 'REJECTED'
            END AS ReqStatus,
            CASE
                WHEN n.N % 5 = 0 THEN 'HIGH'
                WHEN n.N % 5 IN (1,2) THEN 'MEDIUM'
                ELSE 'LOW'
            END AS RiskLevel,
            CONCAT(N'[BULK-', @BatchTag, N'] Request #', n.N) AS Reason,
            CAST(50000 + (n.N * 25000) AS DECIMAL(18,2)) AS ImpactAmount
        FROM N n
        INNER JOIN #VoucherPool vp ON vp.Rn = ((n.N - 1) % @VoucherCount) + 1
    )
    INSERT INTO dbo.VoucherApprovalRequests
    (
        RequestType, VoucherId, BranchId,
        RequestedBy, ApproverId,
        Status, RiskLevel,
        Reason, RejectReason, RequestPayload,
        ImpactAmount, SLADeadline,
        CreatedAt, UpdatedAt
    )
    SELECT
        r.RequestType,
        r.VoucherId,
        bp.Id,
        COALESCE(sp1.Id, @StaffFallback),
        CASE WHEN r.ReqStatus = 'PENDING' THEN NULL ELSE COALESCE(sp2.Id, @StaffFallback) END,
        r.ReqStatus,
        r.RiskLevel,
        r.Reason,
        CASE WHEN r.ReqStatus = 'REJECTED' THEN N'Không đủ điều kiện ngoại lệ' ELSE NULL END,
        CONCAT(N'{"seed":"bulk","tag":"', @BatchTag, N'","seq":', r.N, N'}'),
        r.ImpactAmount,
        DATEADD(HOUR, r.N, @Now),
        DATEADD(MINUTE, -r.N * 3, @Now),
        DATEADD(MINUTE, -r.N * 2, @Now)
    FROM R r
    LEFT JOIN #BranchPool bp ON bp.Rn = ((r.N - 1) % NULLIF(@BranchCount, 0)) + 1
    LEFT JOIN #StaffPool sp1 ON sp1.Rn = ((r.N - 1) % NULLIF(@StaffCount, 0)) + 1
    LEFT JOIN #StaffPool sp2 ON sp2.Rn = ((r.N + 1) % NULLIF(@StaffCount, 0)) + 1
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.VoucherApprovalRequests a
        WHERE a.Reason = r.Reason
    );

    SET @InsertedApprovals = @@ROWCOUNT;

    COMMIT TRAN;

    PRINT 'Bulk voucher seed completed.';
    PRINT 'Batch: ' + @BatchTag;
    PRINT 'Inserted redemptions: ' + CAST(@InsertedRedemptions AS VARCHAR(20));
    PRINT 'Inserted approvals : ' + CAST(@InsertedApprovals AS VARCHAR(20));
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;

    DECLARE @Err NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @Line INT = ERROR_LINE();
    DECLARE @Num INT = ERROR_NUMBER();

    RAISERROR(N'Bulk seed voucher thất bại (line %d, error %d): %s', 16, 1, @Line, @Num, @Err);
END CATCH;
