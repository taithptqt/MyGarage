SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Diagnoses', N'U') IS NULL
    BEGIN
        THROW 50001, N'Bang dbo.Diagnoses khong ton tai.', 1;
    END;

    IF COL_LENGTH(N'dbo.Diagnoses', N'AssignedBayId') IS NULL
    BEGIN
        ALTER TABLE dbo.Diagnoses
            ADD AssignedBayId BIGINT NULL;
    END;

    IF COL_LENGTH(N'dbo.Diagnoses', N'AssistantTechnicianId') IS NULL
    BEGIN
        ALTER TABLE dbo.Diagnoses
            ADD AssistantTechnicianId BIGINT NULL;
    END;

    IF COL_LENGTH(N'dbo.Diagnoses', N'StartedAt') IS NULL
    BEGIN
        ALTER TABLE dbo.Diagnoses
            ADD StartedAt DATETIME NULL;
    END;

    IF COL_LENGTH(N'dbo.Diagnoses', N'FinalApprovedByManagerId') IS NULL
    BEGIN
        ALTER TABLE dbo.Diagnoses
            ADD FinalApprovedByManagerId BIGINT NULL;
    END;

    IF COL_LENGTH(N'dbo.Diagnoses', N'FinalApprovedAt') IS NULL
    BEGIN
        ALTER TABLE dbo.Diagnoses
            ADD FinalApprovedAt DATETIME NULL;
    END;

    IF COL_LENGTH(N'dbo.Diagnoses', N'FinalApprovalNote') IS NULL
    BEGIN
        ALTER TABLE dbo.Diagnoses
            ADD FinalApprovalNote NVARCHAR(MAX) NULL;
    END;

    IF COL_LENGTH(N'dbo.Diagnoses', N'DispatchCollaborationMeta') IS NULL
    BEGIN
        ALTER TABLE dbo.Diagnoses
            ADD DispatchCollaborationMeta NVARCHAR(4000) NULL;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Diagnoses')
          AND name = N'IX_Diagnoses_AssistantTechnicianId'
    )
    BEGIN
        CREATE NONCLUSTERED INDEX IX_Diagnoses_AssistantTechnicianId
            ON dbo.Diagnoses (AssistantTechnicianId);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Diagnoses')
          AND name = N'IX_Diagnoses_AssignedBayId'
    )
    BEGIN
        CREATE NONCLUSTERED INDEX IX_Diagnoses_AssignedBayId
            ON dbo.Diagnoses (AssignedBayId);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Diagnoses')
          AND name = N'IX_Diagnoses_FinalApprovedByManagerId'
    )
    BEGIN
        CREATE NONCLUSTERED INDEX IX_Diagnoses_FinalApprovedByManagerId
            ON dbo.Diagnoses (FinalApprovedByManagerId);
    END;

    IF COL_LENGTH(N'dbo.Diagnoses', N'AssistantTechnicianId') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM sys.foreign_key_columns fkc
            WHERE fkc.parent_object_id = OBJECT_ID(N'dbo.Diagnoses')
              AND fkc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.Diagnoses'), N'AssistantTechnicianId', 'ColumnId')
       )
    BEGIN
        ALTER TABLE dbo.Diagnoses WITH NOCHECK
            ADD CONSTRAINT FK_Diagnoses_AssistantTechnicianId_Staffs
            FOREIGN KEY (AssistantTechnicianId) REFERENCES dbo.Staffs(Id);
    END;

    IF COL_LENGTH(N'dbo.Diagnoses', N'FinalApprovedByManagerId') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM sys.foreign_key_columns fkc
            WHERE fkc.parent_object_id = OBJECT_ID(N'dbo.Diagnoses')
              AND fkc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.Diagnoses'), N'FinalApprovedByManagerId', 'ColumnId')
       )
    BEGIN
        ALTER TABLE dbo.Diagnoses WITH NOCHECK
            ADD CONSTRAINT FK_Diagnoses_FinalApprovedByManagerId_Staffs
            FOREIGN KEY (FinalApprovedByManagerId) REFERENCES dbo.Staffs(Id);
    END;

    IF COL_LENGTH(N'dbo.Diagnoses', N'AssignedBayId') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM sys.foreign_key_columns fkc
            WHERE fkc.parent_object_id = OBJECT_ID(N'dbo.Diagnoses')
              AND fkc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.Diagnoses'), N'AssignedBayId', 'ColumnId')
       )
    BEGIN
        ALTER TABLE dbo.Diagnoses WITH NOCHECK
            ADD CONSTRAINT FK_Diagnoses_AssignedBayId_Bays
            FOREIGN KEY (AssignedBayId) REFERENCES dbo.Bays(Id);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
