SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.Diagnoses', N'AssistantTechnicianId') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD AssistantTechnicianId BIGINT NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'AssignedBayId') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD AssignedBayId BIGINT NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'StartedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD StartedAt DATETIME NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'FinalApprovedByManagerId') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD FinalApprovedByManagerId BIGINT NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'FinalApprovedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD FinalApprovedAt DATETIME NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'FinalApprovalNote') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD FinalApprovalNote NVARCHAR(500) NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Diagnoses_AssistantTechnicianId'
      AND object_id = OBJECT_ID(N'dbo.Diagnoses')
)
BEGIN
    CREATE INDEX IX_Diagnoses_AssistantTechnicianId ON dbo.Diagnoses (AssistantTechnicianId);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Diagnoses_AssignedBayId'
      AND object_id = OBJECT_ID(N'dbo.Diagnoses')
)
BEGIN
    CREATE INDEX IX_Diagnoses_AssignedBayId ON dbo.Diagnoses (AssignedBayId);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Diagnoses_FinalApprovedByManagerId'
      AND object_id = OBJECT_ID(N'dbo.Diagnoses')
)
BEGIN
    CREATE INDEX IX_Diagnoses_FinalApprovedByManagerId ON dbo.Diagnoses (FinalApprovedByManagerId);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Diagnoses_AssistantTechnicianId'
)
BEGIN
    ALTER TABLE dbo.Diagnoses
        ADD CONSTRAINT FK_Diagnoses_AssistantTechnicianId
            FOREIGN KEY (AssistantTechnicianId) REFERENCES dbo.Staffs (Id);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Diagnoses_AssignedBayId'
)
BEGIN
    ALTER TABLE dbo.Diagnoses
        ADD CONSTRAINT FK_Diagnoses_AssignedBayId
            FOREIGN KEY (AssignedBayId) REFERENCES dbo.Bays (Id);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Diagnoses_FinalApprovedByManagerId'
)
BEGIN
    ALTER TABLE dbo.Diagnoses
        ADD CONSTRAINT FK_Diagnoses_FinalApprovedByManagerId
            FOREIGN KEY (FinalApprovedByManagerId) REFERENCES dbo.Staffs (Id);
END;
