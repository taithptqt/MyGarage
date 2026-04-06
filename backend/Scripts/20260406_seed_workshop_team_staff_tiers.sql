SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @TargetBranchId BIGINT = NULL; -- Dat ID chi nhanh neu chi muon cap nhat 1 chi nhanh.
    DECLARE @TargetMembersPerTeam INT = 5;

    DECLARE @RoleTechId BIGINT;
    DECLARE @RoleForemanId BIGINT;
    DECLARE @DefaultPasswordHash VARCHAR(255);

    SELECT TOP (1) @RoleTechId = r.Id
    FROM dbo.Roles AS r
    WHERE ISNULL(r.IsDeleted, 0) = 0
      AND (
            UPPER(ISNULL(r.RoleCode, '')) IN ('TECH', 'TECHNICIAN')
         OR r.RoleName LIKE N'%Ky thuat%'
         OR r.RoleName LIKE N'%Kỹ thuật%'
      )
    ORDER BY CASE WHEN UPPER(ISNULL(r.RoleCode, '')) = 'TECH' THEN 0 ELSE 1 END, r.Id;

    SELECT TOP (1) @RoleForemanId = r.Id
    FROM dbo.Roles AS r
    WHERE ISNULL(r.IsDeleted, 0) = 0
      AND (
            UPPER(ISNULL(r.RoleCode, '')) = 'FOREMAN'
         OR r.RoleName LIKE N'%To truong%'
         OR r.RoleName LIKE N'%Tổ trưởng%'
      )
    ORDER BY CASE WHEN UPPER(ISNULL(r.RoleCode, '')) = 'FOREMAN' THEN 0 ELSE 1 END, r.Id;

    IF @RoleTechId IS NULL
        THROW 50001, 'Khong tim thay role TECH/TECHNICIAN trong bang Roles.', 1;

    SELECT TOP (1) @DefaultPasswordHash = s.PasswordHash
    FROM dbo.Staffs AS s
    WHERE ISNULL(s.IsDeleted, 0) = 0
      AND NULLIF(LTRIM(RTRIM(s.PasswordHash)), '') IS NOT NULL
    ORDER BY CASE WHEN s.RoleId IN (@RoleTechId, ISNULL(@RoleForemanId, -1)) THEN 0 ELSE 1 END, s.Id;

    IF @DefaultPasswordHash IS NULL
        THROW 50002, 'Khong tim thay PasswordHash mau de tao tai khoan tho moi.', 1;

    DECLARE @Branches TABLE (Id BIGINT PRIMARY KEY);

    INSERT INTO @Branches (Id)
    SELECT b.Id
    FROM dbo.Branches AS b
    WHERE ISNULL(b.IsDeleted, 0) = 0
      AND (@TargetBranchId IS NULL OR b.Id = @TargetBranchId);

    IF NOT EXISTS (SELECT 1 FROM @Branches)
        THROW 50003, 'Khong tim thay chi nhanh hop le de cap nhat.', 1;

    DECLARE @TeamSeed TABLE
    (
        TeamCode VARCHAR(50) NOT NULL PRIMARY KEY,
        TeamName NVARCHAR(255) NOT NULL,
        SpecialtyTag VARCHAR(100) NULL,
        SortOrder INT NOT NULL
    );

    INSERT INTO @TeamSeed (TeamCode, TeamName, SpecialtyTag, SortOrder)
    VALUES
        ('ENG_CHAS_1', N'Tổ Máy - Gầm 1', 'ENGINE_CHASSIS', 1),
        ('ENG_CHAS_2', N'Tổ Máy - Gầm 2', 'ENGINE_CHASSIS', 2),
        ('ELEC_AC', N'Tổ Điện - Máy lạnh', 'ELECTRICAL_AC', 3),
        ('FAST_MAINT', N'Tổ Bảo dưỡng nhanh', 'MAINTENANCE', 4),
        ('BODY_CARE', N'Tổ Đồng sơn / Chăm sóc xe', 'BODY_PAINT', 5);

    ;WITH TeamSource AS
    (
        SELECT b.Id AS BranchId,
               ts.TeamCode,
               ts.TeamName,
               ts.SpecialtyTag,
               ts.SortOrder
        FROM @Branches AS b
        CROSS JOIN @TeamSeed AS ts
    )
    MERGE dbo.Teams AS tgt
    USING TeamSource AS src
       ON tgt.BranchId = src.BranchId
      AND tgt.Code = src.TeamCode
    WHEN MATCHED THEN
        UPDATE SET
            tgt.Name = src.TeamName,
            tgt.SpecialtyTag = src.SpecialtyTag,
            tgt.Status = 'ACTIVE',
            tgt.IsDeleted = 0,
            tgt.UpdatedAt = GETDATE()
    WHEN NOT MATCHED THEN
        INSERT (Name, Code, BranchId, SpecialtyTag, LeaderId, Status, Description, CreatedAt, UpdatedAt, IsDeleted)
        VALUES (src.TeamName, src.TeamCode, src.BranchId, src.SpecialtyTag, NULL, 'ACTIVE', N'Tổ kỹ thuật vận hành theo SOP điều phối', GETDATE(), GETDATE(), 0);

    DECLARE @TeamScope TABLE
    (
        TeamId BIGINT PRIMARY KEY,
        BranchId BIGINT NOT NULL,
        TeamCode VARCHAR(50) NOT NULL,
        TeamName NVARCHAR(255) NOT NULL,
        SortOrder INT NOT NULL
    );

    INSERT INTO @TeamScope (TeamId, BranchId, TeamCode, TeamName, SortOrder)
    SELECT t.Id,
           t.BranchId,
           t.Code,
           t.Name,
           ts.SortOrder
    FROM dbo.Teams AS t
    INNER JOIN @Branches AS b
        ON b.Id = t.BranchId
    INNER JOIN @TeamSeed AS ts
        ON ts.TeamCode = t.Code
    WHERE ISNULL(t.IsDeleted, 0) = 0;

    DECLARE @TierSeed TABLE
    (
        TierName NVARCHAR(100) NOT NULL PRIMARY KEY,
        BaseSalary DECIMAL(18, 2) NOT NULL,
        RankOrder INT NOT NULL
    );

    INSERT INTO @TierSeed (TierName, BaseSalary, RankOrder)
    VALUES
        (N'Thợ học việc', 7000000, 1),
        (N'Thợ phụ', 9000000, 2),
        (N'Thợ chính', 12000000, 3),
        (N'Kỹ thuật viên cao cấp', 16000000, 4),
        (N'Tổ trưởng kỹ thuật', 20000000, 5);

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

    DECLARE @TierApprenticeId BIGINT = (SELECT TOP (1) Id FROM dbo.StaffTiers WHERE TierName = N'Thợ học việc' AND ISNULL(IsDeleted, 0) = 0 ORDER BY Id);
    DECLARE @TierHelperId BIGINT = (SELECT TOP (1) Id FROM dbo.StaffTiers WHERE TierName = N'Thợ phụ' AND ISNULL(IsDeleted, 0) = 0 ORDER BY Id);
    DECLARE @TierMainId BIGINT = (SELECT TOP (1) Id FROM dbo.StaffTiers WHERE TierName = N'Thợ chính' AND ISNULL(IsDeleted, 0) = 0 ORDER BY Id);
    DECLARE @TierSeniorId BIGINT = (SELECT TOP (1) Id FROM dbo.StaffTiers WHERE TierName = N'Kỹ thuật viên cao cấp' AND ISNULL(IsDeleted, 0) = 0 ORDER BY Id);
    DECLARE @TierLeadId BIGINT = (SELECT TOP (1) Id FROM dbo.StaffTiers WHERE TierName = N'Tổ trưởng kỹ thuật' AND ISNULL(IsDeleted, 0) = 0 ORDER BY Id);

    UPDATE s
       SET s.StaffTierId = CASE
                              WHEN @RoleForemanId IS NOT NULL AND s.RoleId = @RoleForemanId THEN @TierLeadId
                              WHEN ISNULL(s.SkillLevel, 0) >= 8 THEN @TierSeniorId
                              WHEN ISNULL(s.SkillLevel, 0) >= 6 THEN @TierMainId
                              WHEN ISNULL(s.SkillLevel, 0) >= 4 THEN @TierHelperId
                              ELSE @TierApprenticeId
                           END,
           s.UpdatedAt = GETDATE()
    FROM dbo.Staffs AS s
    INNER JOIN @Branches AS b
        ON b.Id = s.BranchId
    WHERE ISNULL(s.IsDeleted, 0) = 0
      AND UPPER(ISNULL(s.Status, 'ACTIVE')) = 'ACTIVE'
      AND s.RoleId IN (@RoleTechId, ISNULL(@RoleForemanId, -1));

    ;WITH TeamCycle AS
    (
        SELECT ts.TeamId,
               ts.BranchId,
               ROW_NUMBER() OVER (PARTITION BY ts.BranchId ORDER BY ts.SortOrder, ts.TeamId) AS TeamOrder,
               COUNT(*) OVER (PARTITION BY ts.BranchId) AS TeamCount
        FROM @TeamScope AS ts
    ),
    UnassignedTech AS
    (
        SELECT s.Id,
               s.BranchId,
               ROW_NUMBER() OVER (PARTITION BY s.BranchId ORDER BY ISNULL(s.SkillLevel, 0) DESC, s.Id) AS StaffOrder
        FROM dbo.Staffs AS s
        INNER JOIN @Branches AS b
            ON b.Id = s.BranchId
        LEFT JOIN @TeamScope AS ts
            ON ts.TeamId = s.TeamId
        WHERE ISNULL(s.IsDeleted, 0) = 0
          AND UPPER(ISNULL(s.Status, 'ACTIVE')) = 'ACTIVE'
          AND s.RoleId IN (@RoleTechId, ISNULL(@RoleForemanId, -1))
          AND (s.TeamId IS NULL OR ts.TeamId IS NULL)
    )
    UPDATE s
       SET s.TeamId = tc.TeamId,
           s.UpdatedAt = GETDATE()
    FROM dbo.Staffs AS s
    INNER JOIN UnassignedTech AS ut
        ON ut.Id = s.Id
    INNER JOIN TeamCycle AS tc
        ON tc.BranchId = ut.BranchId
       AND tc.TeamOrder = ((ut.StaffOrder - 1) % tc.TeamCount) + 1;

    DECLARE @Deficit TABLE
    (
        TeamId BIGINT PRIMARY KEY,
        BranchId BIGINT NOT NULL,
        TeamCode VARCHAR(50) NOT NULL,
        TeamName NVARCHAR(255) NOT NULL,
        Missing INT NOT NULL,
        ExistingCount INT NOT NULL
    );

    INSERT INTO @Deficit (TeamId, BranchId, TeamCode, TeamName, Missing, ExistingCount)
    SELECT ts.TeamId,
           ts.BranchId,
           ts.TeamCode,
           ts.TeamName,
           CASE WHEN @TargetMembersPerTeam - COUNT(s.Id) > 0 THEN @TargetMembersPerTeam - COUNT(s.Id) ELSE 0 END AS Missing,
           COUNT(s.Id) AS ExistingCount
    FROM @TeamScope AS ts
    LEFT JOIN dbo.Staffs AS s
        ON s.TeamId = ts.TeamId
       AND s.BranchId = ts.BranchId
       AND ISNULL(s.IsDeleted, 0) = 0
       AND UPPER(ISNULL(s.Status, 'ACTIVE')) = 'ACTIVE'
       AND s.RoleId IN (@RoleTechId, ISNULL(@RoleForemanId, -1))
    GROUP BY ts.TeamId, ts.BranchId, ts.TeamCode, ts.TeamName
    HAVING @TargetMembersPerTeam - COUNT(s.Id) > 0;

    ;WITH Num AS
    (
        SELECT TOP (500) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
        FROM sys.all_objects
    ),
    NeedInsert AS
    (
        SELECT d.TeamId,
               d.BranchId,
               d.TeamCode,
               d.TeamName,
               d.ExistingCount + n.N AS SerialNo
        FROM @Deficit AS d
        INNER JOIN Num AS n
            ON n.N <= d.Missing
    )
    INSERT INTO dbo.Staffs
    (
        FullName,
        Phone,
        Email,
        PasswordHash,
        RoleId,
        BranchId,
        TeamId,
        StaffTierId,
        SkillLevel,
        Status,
        CreatedAt,
        UpdatedAt,
        IsDeleted,
        IsLockedOut,
        AccessFailedCount
    )
    SELECT N'Thợ ' + ni.TeamName + N' #' + CAST(ni.SerialNo AS NVARCHAR(10)) AS FullName,
           NULL AS Phone,
           LOWER(CONCAT('tech.', ni.BranchId, '.', ni.TeamCode, '.', RIGHT(CONCAT('000', ni.SerialNo), 3), '@mygarage.local')) AS Email,
           @DefaultPasswordHash AS PasswordHash,
           @RoleTechId AS RoleId,
           ni.BranchId,
           ni.TeamId,
           @TierHelperId,
           4 AS SkillLevel,
           'ACTIVE' AS Status,
           GETDATE() AS CreatedAt,
           GETDATE() AS UpdatedAt,
           0 AS IsDeleted,
           0 AS IsLockedOut,
           0 AS AccessFailedCount
    FROM NeedInsert AS ni;

    ;WITH LeaderCandidate AS
    (
        SELECT ts.TeamId,
               s.Id AS StaffId,
               ISNULL(st.BaseSalary, 0) AS TierSalary,
               ISNULL(s.SkillLevel, 0) AS SkillLevel,
               ROW_NUMBER() OVER (
                   PARTITION BY ts.TeamId
                   ORDER BY ISNULL(st.BaseSalary, 0) DESC,
                            ISNULL(s.SkillLevel, 0) DESC,
                            s.Id ASC
               ) AS RN
        FROM @TeamScope AS ts
        INNER JOIN dbo.Staffs AS s
            ON s.TeamId = ts.TeamId
           AND s.BranchId = ts.BranchId
           AND ISNULL(s.IsDeleted, 0) = 0
           AND UPPER(ISNULL(s.Status, 'ACTIVE')) = 'ACTIVE'
           AND s.RoleId IN (@RoleTechId, ISNULL(@RoleForemanId, -1))
        LEFT JOIN dbo.StaffTiers AS st
            ON st.Id = s.StaffTierId
    ),
    TeamLeader AS
    (
        SELECT TeamId, StaffId
        FROM LeaderCandidate
        WHERE RN = 1
    )
    UPDATE t
       SET t.LeaderId = tl.StaffId,
           t.Status = 'ACTIVE',
           t.IsDeleted = 0,
           t.UpdatedAt = GETDATE()
    FROM dbo.Teams AS t
    INNER JOIN TeamLeader AS tl
        ON tl.TeamId = t.Id;

    IF @RoleForemanId IS NOT NULL
    BEGIN
        UPDATE s
           SET s.RoleId = @RoleForemanId,
               s.StaffTierId = @TierLeadId,
               s.SkillLevel = CASE WHEN ISNULL(s.SkillLevel, 0) < 9 THEN 9 ELSE s.SkillLevel END,
               s.UpdatedAt = GETDATE()
        FROM dbo.Staffs AS s
        INNER JOIN dbo.Teams AS t
            ON t.LeaderId = s.Id
        INNER JOIN @TeamScope AS ts
            ON ts.TeamId = t.Id
        WHERE ISNULL(s.IsDeleted, 0) = 0
          AND UPPER(ISNULL(s.Status, 'ACTIVE')) = 'ACTIVE'
          AND (
                s.RoleId <> @RoleForemanId
             OR ISNULL(s.StaffTierId, -1) <> ISNULL(@TierLeadId, -1)
             OR ISNULL(s.SkillLevel, 0) < 9
          );
    END;

    IF OBJECT_ID(N'dbo.StaffSalaryProfiles', N'U') IS NOT NULL
    BEGIN
        ;WITH SalaryTarget AS
        (
            SELECT s.Id AS StaffId,
                   s.BranchId,
                   CAST(ISNULL(st.BaseSalary, 0) AS DECIMAL(18,2)) AS BaseSalary
            FROM dbo.Staffs AS s
            INNER JOIN @Branches AS b
                ON b.Id = s.BranchId
            LEFT JOIN dbo.StaffTiers AS st
                ON st.Id = s.StaffTierId
            WHERE ISNULL(s.IsDeleted, 0) = 0
              AND UPPER(ISNULL(s.Status, 'ACTIVE')) = 'ACTIVE'
              AND s.RoleId IN (@RoleTechId, ISNULL(@RoleForemanId, -1))
        )
        UPDATE p
           SET p.BaseSalary = st.BaseSalary,
               p.Status = 'ACTIVE',
               p.Note = N'Đồng bộ theo bậc nhân sự kỹ thuật',
               p.UpdatedAt = GETDATE()
        FROM dbo.StaffSalaryProfiles AS p
        INNER JOIN SalaryTarget AS st
            ON st.StaffId = p.StaffId
        WHERE p.EffectiveTo IS NULL
          AND (
                p.BaseSalary <> st.BaseSalary
             OR UPPER(ISNULL(p.Status, '')) <> 'ACTIVE'
             OR ISNULL(p.Note, N'') <> N'Đồng bộ theo bậc nhân sự kỹ thuật'
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
               N'Đồng bộ theo bậc nhân sự kỹ thuật',
               NULL,
               GETDATE(),
               GETDATE()
        FROM SalaryTarget AS st
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.StaffSalaryProfiles AS p
            WHERE p.StaffId = st.StaffId
              AND p.EffectiveTo IS NULL
        );
    END;

    COMMIT TRAN;

    SELECT
        t.BranchId,
        t.Code AS TeamCode,
        t.Name AS TeamName,
        t.SpecialtyTag,
        t.LeaderId,
        leader.FullName AS LeaderName,
        COUNT(s.Id) AS MemberCount
    FROM dbo.Teams AS t
    LEFT JOIN dbo.Staffs AS leader
        ON leader.Id = t.LeaderId
    LEFT JOIN dbo.Staffs AS s
        ON s.TeamId = t.Id
       AND ISNULL(s.IsDeleted, 0) = 0
       AND UPPER(ISNULL(s.Status, 'ACTIVE')) = 'ACTIVE'
       AND s.RoleId IN (@RoleTechId, ISNULL(@RoleForemanId, -1))
    INNER JOIN @TeamSeed AS ts
        ON ts.TeamCode = t.Code
    GROUP BY t.BranchId, t.Code, t.Name, t.SpecialtyTag, t.LeaderId, leader.FullName
    ORDER BY t.BranchId, MIN(ts.SortOrder), t.Name;

    SELECT TierName, BaseSalary
    FROM dbo.StaffTiers
    WHERE TierName IN (N'Thợ học việc', N'Thợ phụ', N'Thợ chính', N'Kỹ thuật viên cao cấp', N'Tổ trưởng kỹ thuật')
      AND ISNULL(IsDeleted, 0) = 0
    ORDER BY BaseSalary;

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;

    THROW;
END CATCH;
