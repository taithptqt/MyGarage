SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Teams', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Teams
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Teams PRIMARY KEY,
        Name NVARCHAR(255) NOT NULL,
        Code VARCHAR(50) NULL,
        BranchId BIGINT NOT NULL,
        SpecialtyTag VARCHAR(100) NULL,
        LeaderId BIGINT NULL,
        Status VARCHAR(50) NULL CONSTRAINT DF_Teams_Status DEFAULT ('ACTIVE'),
        Description NVARCHAR(500) NULL,
        CreatedAt DATETIME NULL CONSTRAINT DF_Teams_CreatedAt DEFAULT (GETDATE()),
        CreatedBy BIGINT NULL,
        UpdatedAt DATETIME NULL CONSTRAINT DF_Teams_UpdatedAt DEFAULT (GETDATE()),
        UpdatedBy BIGINT NULL,
        IsDeleted BIT NULL CONSTRAINT DF_Teams_IsDeleted DEFAULT (0),
        DeletedAt DATETIME NULL,
        DeletedBy BIGINT NULL
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UQ_Teams_BranchId_Code'
      AND object_id = OBJECT_ID(N'dbo.Teams')
)
BEGIN
    CREATE UNIQUE INDEX UQ_Teams_BranchId_Code
        ON dbo.Teams (BranchId, Code)
        WHERE Code IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Teams_BranchId'
)
BEGIN
    ALTER TABLE dbo.Teams
        ADD CONSTRAINT FK_Teams_BranchId
            FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Teams_LeaderId'
)
BEGIN
    ALTER TABLE dbo.Teams
        ADD CONSTRAINT FK_Teams_LeaderId
            FOREIGN KEY (LeaderId) REFERENCES dbo.Staffs (Id);
END;

IF COL_LENGTH(N'dbo.Staffs', N'TeamId') IS NULL
BEGIN
    ALTER TABLE dbo.Staffs
        ADD TeamId BIGINT NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Staffs_TeamId'
      AND object_id = OBJECT_ID(N'dbo.Staffs')
)
BEGIN
    CREATE INDEX IX_Staffs_TeamId ON dbo.Staffs (TeamId);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Staffs_TeamId'
)
BEGIN
    ALTER TABLE dbo.Staffs
        ADD CONSTRAINT FK_Staffs_TeamId
            FOREIGN KEY (TeamId) REFERENCES dbo.Teams (Id);
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'DispatchTag') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD DispatchTag VARCHAR(100) NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'AssignedTeamId') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD AssignedTeamId BIGINT NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'AssignedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD AssignedAt DATETIME NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'SupportRequestedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD SupportRequestedAt DATETIME NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'SupportRequestedBy') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD SupportRequestedBy BIGINT NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'SupportRequestNote') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD SupportRequestNote NVARCHAR(500) NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'CancellationStatus') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses
        ADD CancellationStatus VARCHAR(20) NULL
            CONSTRAINT DF_Diagnoses_CancellationStatus DEFAULT ('NONE');
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'CancellationReason') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD CancellationReason NVARCHAR(500) NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'CancellationRequestedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD CancellationRequestedAt DATETIME NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'CancellationRequestedBy') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD CancellationRequestedBy BIGINT NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'CancellationDecisionAt') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD CancellationDecisionAt DATETIME NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'CancellationDecisionBy') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD CancellationDecisionBy BIGINT NULL;
END;

IF COL_LENGTH(N'dbo.Diagnoses', N'CancellationDecisionNote') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses ADD CancellationDecisionNote NVARCHAR(500) NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Diagnoses_AssignedTeamId'
      AND object_id = OBJECT_ID(N'dbo.Diagnoses')
)
BEGIN
    CREATE INDEX IX_Diagnoses_AssignedTeamId ON dbo.Diagnoses (AssignedTeamId);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Diagnoses_CancellationStatus'
      AND object_id = OBJECT_ID(N'dbo.Diagnoses')
)
BEGIN
    CREATE INDEX IX_Diagnoses_CancellationStatus ON dbo.Diagnoses (CancellationStatus);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Diagnoses_SupportRequestedAt'
      AND object_id = OBJECT_ID(N'dbo.Diagnoses')
)
BEGIN
    CREATE INDEX IX_Diagnoses_SupportRequestedAt ON dbo.Diagnoses (SupportRequestedAt);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Diagnoses_AssignedTeamId'
)
BEGIN
    ALTER TABLE dbo.Diagnoses
        ADD CONSTRAINT FK_Diagnoses_AssignedTeamId
            FOREIGN KEY (AssignedTeamId) REFERENCES dbo.Teams (Id);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Diagnoses_SupportRequestedBy'
)
BEGIN
    ALTER TABLE dbo.Diagnoses
        ADD CONSTRAINT FK_Diagnoses_SupportRequestedBy
            FOREIGN KEY (SupportRequestedBy) REFERENCES dbo.Staffs (Id);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Diagnoses_CancellationRequestedBy'
)
BEGIN
    ALTER TABLE dbo.Diagnoses
        ADD CONSTRAINT FK_Diagnoses_CancellationRequestedBy
            FOREIGN KEY (CancellationRequestedBy) REFERENCES dbo.Staffs (Id);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Diagnoses_CancellationDecisionBy'
)
BEGIN
    ALTER TABLE dbo.Diagnoses
        ADD CONSTRAINT FK_Diagnoses_CancellationDecisionBy
            FOREIGN KEY (CancellationDecisionBy) REFERENCES dbo.Staffs (Id);
END;
