SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID(N'dbo.DiagnosisAssignments', N'U') IS NULL
    BEGIN
        THROW 50000, N'Table dbo.DiagnosisAssignments not found.', 1;
    END

    IF COL_LENGTH(N'dbo.DiagnosisAssignments', N'TechnicalApprovalStatus') IS NOT NULL
       AND COL_LENGTH(N'dbo.DiagnosisAssignments', N'TeamLeaderApprovalStatus') IS NULL
    BEGIN
        EXEC sp_rename
            @objname = N'dbo.DiagnosisAssignments.TechnicalApprovalStatus',
            @newname = N'TeamLeaderApprovalStatus',
            @objtype = N'COLUMN';
    END

    IF COL_LENGTH(N'dbo.DiagnosisAssignments', N'TeamLeaderApprovalStatus') IS NULL
        ALTER TABLE dbo.DiagnosisAssignments ADD TeamLeaderApprovalStatus VARCHAR(20) NULL;
    IF COL_LENGTH(N'dbo.DiagnosisAssignments', N'FinalApprovalStatus') IS NULL
        ALTER TABLE dbo.DiagnosisAssignments ADD FinalApprovalStatus VARCHAR(20) NULL;
    IF COL_LENGTH(N'dbo.DiagnosisAssignments', N'ForemanRejectReason') IS NULL
        ALTER TABLE dbo.DiagnosisAssignments ADD ForemanRejectReason NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.DiagnosisAssignments', N'ApprovedByForemanId') IS NULL
        ALTER TABLE dbo.DiagnosisAssignments ADD ApprovedByForemanId BIGINT NULL;
    IF COL_LENGTH(N'dbo.DiagnosisAssignments', N'ApprovedByForemanAt') IS NULL
        ALTER TABLE dbo.DiagnosisAssignments ADD ApprovedByForemanAt DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.DiagnosisAssignments', N'ApprovedByTeamLeaderId') IS NULL
        ALTER TABLE dbo.DiagnosisAssignments ADD ApprovedByTeamLeaderId BIGINT NULL;
    IF COL_LENGTH(N'dbo.DiagnosisAssignments', N'ApprovedByTeamLeaderAt') IS NULL
        ALTER TABLE dbo.DiagnosisAssignments ADD ApprovedByTeamLeaderAt DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.DiagnosisAssignments', N'TeamLeaderNote') IS NULL
        ALTER TABLE dbo.DiagnosisAssignments ADD TeamLeaderNote NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'dbo.DiagnosisAssignments', N'ForemanNote') IS NULL
        ALTER TABLE dbo.DiagnosisAssignments ADD ForemanNote NVARCHAR(1000) NULL;

    IF OBJECT_ID(N'dbo.Diagnoses', N'U') IS NOT NULL
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM sys.check_constraints cc
            INNER JOIN sys.tables t ON t.object_id = cc.parent_object_id
            WHERE cc.name = N'CK_Status_Enum'
              AND t.name = N'Diagnoses'
              AND SCHEMA_NAME(t.schema_id) = N'dbo'
        )
        BEGIN
            ALTER TABLE dbo.Diagnoses DROP CONSTRAINT CK_Status_Enum;
        END

        ALTER TABLE dbo.Diagnoses
            ADD CONSTRAINT CK_Status_Enum
            CHECK ([Status] IN ('PENDING','ASSIGNED','COMPLETED','CANCELED'));
    END

    DECLARE @dropSql NVARCHAR(MAX) = N'';

    ;WITH DropTargets AS
    (
        SELECT N'TechnicalApprovalStatus' AS ColName UNION ALL
        SELECT N'TeamLeaderApprovalStatus' UNION ALL
        SELECT N'FinalApprovalStatus' UNION ALL
        SELECT N'ForemanRejectReason' UNION ALL
        SELECT N'ApprovedByForemanId' UNION ALL
        SELECT N'ApprovedByForemanAt' UNION ALL
        SELECT N'ApprovedByTeamLeaderId' UNION ALL
        SELECT N'ApprovedByTeamLeaderAt' UNION ALL
        SELECT N'TeamLeaderNote' UNION ALL
        SELECT N'ForemanNote' UNION ALL
        SELECT N'DispatchCollaborationMeta'
    )
    SELECT @dropSql = @dropSql +
        N'ALTER TABLE dbo.Diagnoses DROP CONSTRAINT [' + dc.name + N'];' + CHAR(10)
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    INNER JOIN DropTargets t
        ON t.ColName = c.name
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Diagnoses');

    ;WITH DropTargets AS
    (
        SELECT N'TechnicalApprovalStatus' AS ColName UNION ALL
        SELECT N'TeamLeaderApprovalStatus' UNION ALL
        SELECT N'FinalApprovalStatus' UNION ALL
        SELECT N'ForemanRejectReason' UNION ALL
        SELECT N'ApprovedByForemanId' UNION ALL
        SELECT N'ApprovedByForemanAt' UNION ALL
        SELECT N'ApprovedByTeamLeaderId' UNION ALL
        SELECT N'ApprovedByTeamLeaderAt' UNION ALL
        SELECT N'TeamLeaderNote' UNION ALL
        SELECT N'ForemanNote' UNION ALL
        SELECT N'DispatchCollaborationMeta'
    )
    SELECT @dropSql = @dropSql +
        N'ALTER TABLE dbo.Diagnoses DROP CONSTRAINT [' + fk.name + N'];' + CHAR(10)
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    INNER JOIN sys.columns c
        ON c.object_id = fkc.parent_object_id
       AND c.column_id = fkc.parent_column_id
    INNER JOIN DropTargets t
        ON t.ColName = c.name
    WHERE fk.parent_object_id = OBJECT_ID(N'dbo.Diagnoses');

    ;WITH DropTargets AS
    (
        SELECT N'TechnicalApprovalStatus' AS ColName UNION ALL
        SELECT N'TeamLeaderApprovalStatus' UNION ALL
        SELECT N'FinalApprovalStatus' UNION ALL
        SELECT N'ForemanRejectReason' UNION ALL
        SELECT N'ApprovedByForemanId' UNION ALL
        SELECT N'ApprovedByForemanAt' UNION ALL
        SELECT N'ApprovedByTeamLeaderId' UNION ALL
        SELECT N'ApprovedByTeamLeaderAt' UNION ALL
        SELECT N'TeamLeaderNote' UNION ALL
        SELECT N'ForemanNote' UNION ALL
        SELECT N'DispatchCollaborationMeta'
    )
    SELECT @dropSql = @dropSql +
        N'DROP INDEX [' + i.name + N'] ON dbo.Diagnoses;' + CHAR(10)
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic
        ON ic.object_id = i.object_id
       AND ic.index_id = i.index_id
    INNER JOIN sys.columns c
        ON c.object_id = ic.object_id
       AND c.column_id = ic.column_id
    INNER JOIN DropTargets t
        ON t.ColName = c.name
    WHERE i.object_id = OBJECT_ID(N'dbo.Diagnoses')
      AND i.is_primary_key = 0
      AND i.is_unique_constraint = 0;

    IF LEN(@dropSql) > 0
    BEGIN
        EXEC sp_executesql @dropSql;
    END

    IF COL_LENGTH(N'dbo.Diagnoses', N'TechnicalApprovalStatus') IS NOT NULL
        ALTER TABLE dbo.Diagnoses DROP COLUMN TechnicalApprovalStatus;
    IF COL_LENGTH(N'dbo.Diagnoses', N'TeamLeaderApprovalStatus') IS NOT NULL
        ALTER TABLE dbo.Diagnoses DROP COLUMN TeamLeaderApprovalStatus;
    IF COL_LENGTH(N'dbo.Diagnoses', N'FinalApprovalStatus') IS NOT NULL
        ALTER TABLE dbo.Diagnoses DROP COLUMN FinalApprovalStatus;
    IF COL_LENGTH(N'dbo.Diagnoses', N'ForemanRejectReason') IS NOT NULL
        ALTER TABLE dbo.Diagnoses DROP COLUMN ForemanRejectReason;
    IF COL_LENGTH(N'dbo.Diagnoses', N'ApprovedByForemanId') IS NOT NULL
        ALTER TABLE dbo.Diagnoses DROP COLUMN ApprovedByForemanId;
    IF COL_LENGTH(N'dbo.Diagnoses', N'ApprovedByForemanAt') IS NOT NULL
        ALTER TABLE dbo.Diagnoses DROP COLUMN ApprovedByForemanAt;
    IF COL_LENGTH(N'dbo.Diagnoses', N'ApprovedByTeamLeaderId') IS NOT NULL
        ALTER TABLE dbo.Diagnoses DROP COLUMN ApprovedByTeamLeaderId;
    IF COL_LENGTH(N'dbo.Diagnoses', N'ApprovedByTeamLeaderAt') IS NOT NULL
        ALTER TABLE dbo.Diagnoses DROP COLUMN ApprovedByTeamLeaderAt;
    IF COL_LENGTH(N'dbo.Diagnoses', N'TeamLeaderNote') IS NOT NULL
        ALTER TABLE dbo.Diagnoses DROP COLUMN TeamLeaderNote;
    IF COL_LENGTH(N'dbo.Diagnoses', N'ForemanNote') IS NOT NULL
        ALTER TABLE dbo.Diagnoses DROP COLUMN ForemanNote;
    IF COL_LENGTH(N'dbo.Diagnoses', N'DispatchCollaborationMeta') IS NOT NULL
        ALTER TABLE dbo.Diagnoses DROP COLUMN DispatchCollaborationMeta;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRAN;

    THROW;
END CATCH;
