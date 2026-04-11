SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @TargetBranchId BIGINT = NULL; -- Dat ID chi nhanh neu chi muon cap nhat 1 chi nhanh.

    -- Role IDs
    DECLARE @RoleAdminId BIGINT = (
        SELECT TOP (1) Id
        FROM dbo.Roles
        WHERE ISNULL(IsDeleted, 0) = 0
          AND UPPER(ISNULL(RoleCode, '')) = 'ADMIN'
        ORDER BY Id
    );

    DECLARE @RoleReceptionId BIGINT = (
        SELECT TOP (1) Id
        FROM dbo.Roles
        WHERE ISNULL(IsDeleted, 0) = 0
          AND UPPER(ISNULL(RoleCode, '')) = 'RECEPTIONIST'
        ORDER BY Id
    );

    DECLARE @RoleTechId BIGINT = (
        SELECT TOP (1) Id
        FROM dbo.Roles
        WHERE ISNULL(IsDeleted, 0) = 0
          AND UPPER(ISNULL(RoleCode, '')) IN ('TECH', 'TECHNICIAN')
        ORDER BY CASE WHEN UPPER(ISNULL(RoleCode, '')) = 'TECH' THEN 0 ELSE 1 END, Id
    );

    DECLARE @RoleForemanId BIGINT = (
        SELECT TOP (1) Id
        FROM dbo.Roles
        WHERE ISNULL(IsDeleted, 0) = 0
          AND UPPER(ISNULL(RoleCode, '')) = 'FOREMAN'
        ORDER BY Id
    );

    DECLARE @RoleQcId BIGINT = (
        SELECT TOP (1) Id
        FROM dbo.Roles
        WHERE ISNULL(IsDeleted, 0) = 0
          AND UPPER(ISNULL(RoleCode, '')) = 'QUALITY_CONTROL'
        ORDER BY Id
    );

    DECLARE @RoleAccountantId BIGINT = (
        SELECT TOP (1) Id
        FROM dbo.Roles
        WHERE ISNULL(IsDeleted, 0) = 0
          AND UPPER(ISNULL(RoleCode, '')) = 'ACCOUNTANT'
        ORDER BY Id
    );

    DECLARE @RoleManagerId BIGINT = (
        SELECT TOP (1) Id
        FROM dbo.Roles
        WHERE ISNULL(IsDeleted, 0) = 0
          AND UPPER(ISNULL(RoleCode, '')) = 'MANAGER'
        ORDER BY Id
    );

    -- Chuan hoa ten role tieng Viet
    UPDATE dbo.Roles
       SET RoleName = CASE UPPER(ISNULL(RoleCode, ''))
                        WHEN 'ADMIN' THEN N'Quản trị hệ thống'
                        WHEN 'RECEPTIONIST' THEN N'Lễ tân - Cố vấn dịch vụ'
                        WHEN 'TECH' THEN N'Kỹ thuật viên'
                        WHEN 'TECHNICIAN' THEN N'Kỹ thuật viên'
                        WHEN 'FOREMAN' THEN N'Quản đốc'
                        WHEN 'QUALITY_CONTROL' THEN N'Kiểm soát chất lượng'
                        WHEN 'ACCOUNTANT' THEN N'Kế toán'
                        WHEN 'MANAGER' THEN N'Quản lý chi nhánh'
                        ELSE RoleName
                      END,
           UpdatedAt = GETDATE()
    WHERE ISNULL(IsDeleted, 0) = 0
      AND UPPER(ISNULL(RoleCode, '')) IN
          ('ADMIN', 'RECEPTIONIST', 'TECH', 'TECHNICIAN', 'FOREMAN', 'QUALITY_CONTROL', 'ACCOUNTANT', 'MANAGER');

    DECLARE @Branches TABLE (Id BIGINT PRIMARY KEY);

    INSERT INTO @Branches (Id)
    SELECT b.Id
    FROM dbo.Branches AS b
    WHERE ISNULL(b.IsDeleted, 0) = 0
      AND (@TargetBranchId IS NULL OR b.Id = @TargetBranchId);

    IF NOT EXISTS (SELECT 1 FROM @Branches)
        THROW 51001, 'Khong tim thay chi nhanh hop le.', 1;

    -- Chuan hoa ten cap bac ADMIN de tranh trung nghia voi RoleName.
    UPDATE dbo.StaffTiers
       SET TierName = N'Trưởng hệ thống',
           UpdatedAt = GETDATE()
     WHERE TierName = N'Quản trị hệ thống'
       AND ISNULL(IsDeleted, 0) = 0;

    DECLARE @TierSeed TABLE
    (
        TierName NVARCHAR(100) NOT NULL PRIMARY KEY,
        BaseSalary DECIMAL(18, 2) NOT NULL,
        TierGroup VARCHAR(50) NOT NULL,
        RankOrder INT NOT NULL
    );

    INSERT INTO @TierSeed (TierName, BaseSalary, TierGroup, RankOrder)
    VALUES
        (N'Thợ học việc',                7000000,  'TECH',        1),
        (N'Thợ phụ',                     9000000,  'TECH',        2),
        (N'Thợ chính',                  12000000,  'TECH',        3),
        (N'Kỹ thuật viên cao cấp',      16000000,  'TECH',        4),
        (N'Tổ trưởng kỹ thuật',         20000000,  'FOREMAN',     5),
        (N'Quản đốc xưởng',             24000000,  'FOREMAN',     6),

        (N'Lễ tân tập sự',               8000000,  'RECEPTION',   1),
        (N'Lễ tân chính',               11000000,  'RECEPTION',   2),
        (N'Cố vấn dịch vụ cao cấp',     15000000,  'RECEPTION',   3),

        (N'QC tập sự',                   9000000,  'QC',          1),
        (N'QC chính',                   13000000,  'QC',          2),
        (N'Trưởng nhóm QC',             17000000,  'QC',          3),

        (N'Kế toán viên',               12000000,  'ACCOUNTING',  1),
        (N'Kế toán tổng hợp',           17000000,  'ACCOUNTING',  2),
        (N'Kế toán trưởng',             24000000,  'ACCOUNTING',  3),

        (N'Chuyên viên hệ thống',       16000000,  'ADMIN',       1),
        (N'Trưởng hệ thống',            24000000,  'ADMIN',       2),

        (N'Quản lý chi nhánh',          30000000,  'MANAGER',     1),
        (N'Giám đốc vận hành chi nhánh',40000000,  'MANAGER',     2);

    MERGE dbo.StaffTiers AS tgt
    USING @TierSeed AS src
       ON tgt.TierName = src.TierName
    WHEN MATCHED THEN
        UPDATE SET
            tgt.BaseSalary = src.BaseSalary,
            tgt.IsDeleted = 0,
            tgt.UpdatedAt = GETDATE()
    WHEN NOT MATCHED THEN
        INSERT (TierName, BaseSalary, CreatedAt, UpdatedAt, IsDeleted)
        VALUES (src.TierName, src.BaseSalary, GETDATE(), GETDATE(), 0);

    -- Lay Tier IDs
    DECLARE @TierApprenticeId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Thợ học việc' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierHelperId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Thợ phụ' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierMainId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Thợ chính' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierSeniorTechId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Kỹ thuật viên cao cấp' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierLeadId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Tổ trưởng kỹ thuật' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierForemanId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Quản đốc xưởng' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);

    DECLARE @TierReceptionJuniorId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Lễ tân tập sự' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierReceptionMainId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Lễ tân chính' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierReceptionSeniorId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Cố vấn dịch vụ cao cấp' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);

    DECLARE @TierQcJuniorId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'QC tập sự' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierQcMainId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'QC chính' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierQcLeadId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Trưởng nhóm QC' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);

    DECLARE @TierAccJuniorId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Kế toán viên' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierAccMainId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Kế toán tổng hợp' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierAccLeadId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Kế toán trưởng' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);

    DECLARE @TierAdminMainId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Chuyên viên hệ thống' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierAdminLeadId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Trưởng hệ thống' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);

    DECLARE @TierManagerMainId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Quản lý chi nhánh' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);
    DECLARE @TierManagerLeadId BIGINT = (SELECT TOP 1 Id FROM dbo.StaffTiers WHERE TierName = N'Giám đốc vận hành chi nhánh' AND ISNULL(IsDeleted,0)=0 ORDER BY Id);

    ;WITH StaffScope AS
    (
        SELECT s.Id,
               s.RoleId,
               s.BranchId,
               CASE
                    WHEN ISNULL(s.SkillLevel, 0) > 0 THEN ISNULL(s.SkillLevel, 0)
                    WHEN s.RoleId = @RoleManagerId THEN 8
                    WHEN s.RoleId = @RoleAdminId THEN 6
                    WHEN s.RoleId = @RoleAccountantId THEN 6
                    WHEN s.RoleId = @RoleReceptionId THEN 5
                    WHEN s.RoleId = @RoleQcId THEN 5
                    WHEN s.RoleId = @RoleForemanId THEN 8
                    ELSE 4
               END AS EffectiveSkill
        FROM dbo.Staffs AS s
        WHERE ISNULL(s.IsDeleted, 0) = 0
          AND UPPER(ISNULL(s.Status, 'ACTIVE')) = 'ACTIVE'
          AND (
                @TargetBranchId IS NULL
                OR s.BranchId = @TargetBranchId
          )
          AND s.RoleId IN (
                ISNULL(@RoleTechId, -1),
                ISNULL(@RoleForemanId, -1),
                ISNULL(@RoleReceptionId, -1),
                ISNULL(@RoleQcId, -1),
                ISNULL(@RoleAccountantId, -1),
                ISNULL(@RoleAdminId, -1),
                ISNULL(@RoleManagerId, -1)
          )
    )
    UPDATE s
       SET s.StaffTierId = CASE
            WHEN ss.RoleId = @RoleTechId THEN
                CASE
                    WHEN ss.EffectiveSkill >= 8 THEN @TierSeniorTechId
                    WHEN ss.EffectiveSkill >= 6 THEN @TierMainId
                    WHEN ss.EffectiveSkill >= 4 THEN @TierHelperId
                    ELSE @TierApprenticeId
                END
            WHEN ss.RoleId = @RoleForemanId THEN
                CASE WHEN ss.EffectiveSkill >= 10 THEN @TierForemanId ELSE @TierLeadId END
            WHEN ss.RoleId = @RoleReceptionId THEN
                CASE
                    WHEN ss.EffectiveSkill >= 8 THEN @TierReceptionSeniorId
                    WHEN ss.EffectiveSkill >= 5 THEN @TierReceptionMainId
                    ELSE @TierReceptionJuniorId
                END
            WHEN ss.RoleId = @RoleQcId THEN
                CASE
                    WHEN ss.EffectiveSkill >= 8 THEN @TierQcLeadId
                    WHEN ss.EffectiveSkill >= 5 THEN @TierQcMainId
                    ELSE @TierQcJuniorId
                END
            WHEN ss.RoleId = @RoleAccountantId THEN
                CASE
                    WHEN ss.EffectiveSkill >= 8 THEN @TierAccLeadId
                    WHEN ss.EffectiveSkill >= 5 THEN @TierAccMainId
                    ELSE @TierAccJuniorId
                END
            WHEN ss.RoleId = @RoleAdminId THEN
                CASE WHEN ss.EffectiveSkill >= 8 THEN @TierAdminLeadId ELSE @TierAdminMainId END
            WHEN ss.RoleId = @RoleManagerId THEN
                CASE WHEN ss.EffectiveSkill >= 8 THEN @TierManagerLeadId ELSE @TierManagerMainId END
            ELSE s.StaffTierId
       END,
       s.UpdatedAt = GETDATE()
    FROM dbo.Staffs AS s
    INNER JOIN StaffScope AS ss ON ss.Id = s.Id;

    -- Dong bo luong co ban theo tier
    DECLARE @SalaryTarget TABLE
    (
        StaffId BIGINT PRIMARY KEY,
        BranchId BIGINT NULL,
        BaseSalary DECIMAL(18,2) NOT NULL
    );

    INSERT INTO @SalaryTarget (StaffId, BranchId, BaseSalary)
    SELECT s.Id AS StaffId,
           s.BranchId,
           CAST(ISNULL(st.BaseSalary, 0) AS DECIMAL(18,2)) AS BaseSalary
    FROM dbo.Staffs AS s
    LEFT JOIN dbo.StaffTiers AS st
        ON st.Id = s.StaffTierId
    WHERE ISNULL(s.IsDeleted, 0) = 0
      AND UPPER(ISNULL(s.Status, 'ACTIVE')) = 'ACTIVE'
      AND (
            (@TargetBranchId IS NULL AND s.BranchId IS NOT NULL)
            OR (@TargetBranchId IS NOT NULL AND s.BranchId = @TargetBranchId)
      )
      AND s.RoleId IN (
            ISNULL(@RoleTechId, -1), ISNULL(@RoleForemanId, -1), ISNULL(@RoleReceptionId, -1),
            ISNULL(@RoleQcId, -1), ISNULL(@RoleAccountantId, -1), ISNULL(@RoleAdminId, -1), ISNULL(@RoleManagerId, -1)
      );

    IF OBJECT_ID(N'dbo.staff_salary_profiles', N'U') IS NOT NULL
    BEGIN
        UPDATE p
           SET p.base_salary = st.BaseSalary,
               p.status = 'ACTIVE',
               p.note = N'Đồng bộ theo cấp bậc nhân sự',
               p.updated_at = GETDATE()
        FROM dbo.staff_salary_profiles AS p
        INNER JOIN @SalaryTarget AS st ON st.StaffId = p.staff_id
        WHERE p.effective_to IS NULL
          AND (
                p.base_salary <> st.BaseSalary
             OR UPPER(ISNULL(p.status, '')) <> 'ACTIVE'
             OR ISNULL(p.note, N'') <> N'Đồng bộ theo cấp bậc nhân sự'
          );

        INSERT INTO dbo.staff_salary_profiles
        (
            staff_id,
            branch_id,
            base_salary,
            effective_from,
            effective_to,
            status,
            note,
            updated_by,
            created_at,
            updated_at
        )
        SELECT st.StaffId,
               st.BranchId,
               st.BaseSalary,
               CAST(GETDATE() AS DATE),
               NULL,
               'ACTIVE',
               N'Đồng bộ theo cấp bậc nhân sự',
               NULL,
               GETDATE(),
               GETDATE()
        FROM @SalaryTarget AS st
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.staff_salary_profiles AS p
            WHERE p.staff_id = st.StaffId
              AND p.effective_to IS NULL
        );
    END
    ELSE IF OBJECT_ID(N'dbo.StaffSalaryProfiles', N'U') IS NOT NULL
    BEGIN
        UPDATE p
           SET p.BaseSalary = st.BaseSalary,
               p.Status = 'ACTIVE',
               p.Note = N'Đồng bộ theo cấp bậc nhân sự',
               p.UpdatedAt = GETDATE()
        FROM dbo.StaffSalaryProfiles AS p
        INNER JOIN @SalaryTarget AS st ON st.StaffId = p.StaffId
        WHERE p.EffectiveTo IS NULL
          AND (
                p.BaseSalary <> st.BaseSalary
             OR UPPER(ISNULL(p.Status, '')) <> 'ACTIVE'
             OR ISNULL(p.Note, N'') <> N'Đồng bộ theo cấp bậc nhân sự'
          );

        INSERT INTO dbo.StaffSalaryProfiles
        (
            StaffId,
            BranchId,
            BaseSalary,
            EffectiveFrom,
            EffectiveTo,
            Status,
            Note,
            UpdatedBy,
            CreatedAt,
            UpdatedAt
        )
        SELECT st.StaffId,
               st.BranchId,
               st.BaseSalary,
               CAST(GETDATE() AS DATE),
               NULL,
               'ACTIVE',
               N'Đồng bộ theo cấp bậc nhân sự',
               NULL,
               GETDATE(),
               GETDATE()
        FROM @SalaryTarget AS st
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.StaffSalaryProfiles AS p
            WHERE p.StaffId = st.StaffId
              AND p.EffectiveTo IS NULL
        );
    END;

    COMMIT TRAN;

    -- Bao cao ket qua
    SELECT r.RoleCode,
           r.RoleName,
           t.TierName,
           t.BaseSalary,
           COUNT(s.Id) AS StaffCount
    FROM dbo.Staffs AS s
    INNER JOIN dbo.Roles AS r ON r.Id = s.RoleId
    LEFT JOIN dbo.StaffTiers AS t ON t.Id = s.StaffTierId
    INNER JOIN @Branches AS b ON b.Id = s.BranchId
    WHERE ISNULL(s.IsDeleted, 0) = 0
      AND UPPER(ISNULL(s.Status, 'ACTIVE')) = 'ACTIVE'
      AND s.RoleId IN (
            ISNULL(@RoleTechId, -1), ISNULL(@RoleForemanId, -1), ISNULL(@RoleReceptionId, -1),
            ISNULL(@RoleQcId, -1), ISNULL(@RoleAccountantId, -1), ISNULL(@RoleAdminId, -1), ISNULL(@RoleManagerId, -1)
      )
    GROUP BY r.RoleCode, r.RoleName, t.TierName, t.BaseSalary
    ORDER BY r.RoleCode, t.BaseSalary;

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;
    THROW;
END CATCH;
