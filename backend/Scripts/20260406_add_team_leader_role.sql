SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @Now DATETIME = GETDATE();
    DECLARE @TeamLeaderRoleId BIGINT;
    DECLARE @RoleCodeConstraintDefinition NVARCHAR(MAX);

    SELECT @RoleCodeConstraintDefinition = cc.definition
    FROM sys.check_constraints AS cc
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Roles')
      AND cc.name = N'CK_Roles_RoleCode';

    IF @RoleCodeConstraintDefinition IS NOT NULL
       AND UPPER(@RoleCodeConstraintDefinition) NOT LIKE '%TEAM_LEADER%'
    BEGIN
        ALTER TABLE dbo.Roles DROP CONSTRAINT CK_Roles_RoleCode;

        ALTER TABLE dbo.Roles WITH CHECK ADD CONSTRAINT CK_Roles_RoleCode
        CHECK (
            [RoleCode] = 'ACCOUNTANT'
            OR [RoleCode] = 'QUALITY_CONTROL'
            OR [RoleCode] = 'FOREMAN'
            OR [RoleCode] = 'TEAM_LEADER'
            OR [RoleCode] = 'MANAGER'
            OR [RoleCode] = 'RECEPTIONIST'
            OR [RoleCode] = 'TECH'
            OR [RoleCode] = 'ADMIN'
        );
    END;

    SELECT TOP (1) @TeamLeaderRoleId = r.Id
    FROM dbo.Roles AS r
    WHERE UPPER(ISNULL(r.RoleCode, '')) = 'TEAM_LEADER'
    ORDER BY r.Id;

    IF @TeamLeaderRoleId IS NULL
    BEGIN
        INSERT INTO dbo.Roles
        (
            RoleName,
            RoleCode,
            Scope,
            Description,
            IsActive,
            CreatedAt,
            UpdatedAt,
            IsDeleted
        )
        VALUES
        (
            N'To truong',
            'TEAM_LEADER',
            'BRANCH',
            N'Vai tro to truong phu trach dieu phoi theo to.',
            1,
            @Now,
            @Now,
            0
        );

        SET @TeamLeaderRoleId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE dbo.Roles
           SET RoleName = N'To truong',
               Scope = CASE
                         WHEN NULLIF(LTRIM(RTRIM(ISNULL(Scope, ''))), '') IS NULL THEN 'BRANCH'
                         ELSE CASE
                                WHEN UPPER(LTRIM(RTRIM(ISNULL(Scope, '')))) IN ('BRANCH', 'COMPANY')
                                  THEN Scope
                                ELSE 'BRANCH'
                              END
                       END,
               Description = N'Vai tro to truong phu trach dieu phoi theo to.',
               IsActive = 1,
               IsDeleted = 0,
               DeletedAt = NULL,
               DeletedBy = NULL,
               UpdatedAt = @Now
         WHERE Id = @TeamLeaderRoleId;
    END;

    COMMIT TRAN;

    SELECT TOP (1)
           Id,
           RoleName,
           RoleCode,
           Scope,
           IsActive,
           IsDeleted,
           UpdatedAt
    FROM dbo.Roles
    WHERE Id = @TeamLeaderRoleId;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;

    THROW;
END CATCH;
