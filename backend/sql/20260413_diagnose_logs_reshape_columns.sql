/*
  DiagnosisAssignments reshape to parent-child assignment history (2026-04-13)
  - Ensure DiagnosisAssignments columns follow latest spec
  - Rename legacy columns
  - Drop deprecated MetaJson column
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.DiagnosisAssignments', N'U') IS NULL
BEGIN
    RETURN;
END;

IF COL_LENGTH('dbo.DiagnosisAssignments', 'DiagnoseId') IS NOT NULL
   AND COL_LENGTH('dbo.DiagnosisAssignments', 'DiagnosisId') IS NULL
BEGIN
    EXEC sp_rename 'dbo.DiagnosisAssignments.DiagnoseId', 'DiagnosisId', 'COLUMN';
END;

IF COL_LENGTH('dbo.DiagnosisAssignments', 'TeamId') IS NOT NULL
   AND COL_LENGTH('dbo.DiagnosisAssignments', 'AssignedTeamId') IS NULL
BEGIN
    EXEC sp_rename 'dbo.DiagnosisAssignments.TeamId', 'AssignedTeamId', 'COLUMN';
END;

IF COL_LENGTH('dbo.DiagnosisAssignments', 'BayId') IS NOT NULL
   AND COL_LENGTH('dbo.DiagnosisAssignments', 'AssignedBayId') IS NULL
BEGIN
    EXEC sp_rename 'dbo.DiagnosisAssignments.BayId', 'AssignedBayId', 'COLUMN';
END;

IF COL_LENGTH('dbo.DiagnosisAssignments', 'StatusTo') IS NOT NULL
   AND COL_LENGTH('dbo.DiagnosisAssignments', 'Status') IS NULL
BEGIN
    EXEC sp_rename 'dbo.DiagnosisAssignments.StatusTo', 'Status', 'COLUMN';
END;

IF COL_LENGTH('dbo.DiagnosisAssignments', 'StatusFrom') IS NOT NULL
   AND COL_LENGTH('dbo.DiagnosisAssignments', 'OldStatus') IS NULL
BEGIN
    EXEC sp_rename 'dbo.DiagnosisAssignments.StatusFrom', 'OldStatus', 'COLUMN';
END;

IF COL_LENGTH('dbo.DiagnosisAssignments', 'CancelReason') IS NOT NULL
   AND COL_LENGTH('dbo.DiagnosisAssignments', 'CancellationReason') IS NULL
BEGIN
    EXEC sp_rename 'dbo.DiagnosisAssignments.CancelReason', 'CancellationReason', 'COLUMN';
END;

IF COL_LENGTH('dbo.DiagnosisAssignments', 'MetaJson') IS NOT NULL
BEGIN
    ALTER TABLE dbo.DiagnosisAssignments DROP COLUMN MetaJson;
END;

IF COL_LENGTH('dbo.DiagnosisAssignments', 'AssignedAt') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD AssignedAt datetime2 NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'TechnicianAcceptedAt') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD TechnicianAcceptedAt datetime2 NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'TechnicianCompletedAt') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD TechnicianCompletedAt datetime2 NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'ApprovedByTeamLeaderAt') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD ApprovedByTeamLeaderAt datetime2 NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'TeamLeaderNote') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD TeamLeaderNote nvarchar(500) NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'ApprovedByForemanId') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD ApprovedByForemanId bigint NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'ApprovedByForemanAt') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD ApprovedByForemanAt datetime2 NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'ForemanNote') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD ForemanNote nvarchar(500) NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'CancellationStatus') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD CancellationStatus varchar(20) NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'CancellationRequestedBy') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD CancellationRequestedBy bigint NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'CancellationRequestedAt') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD CancellationRequestedAt datetime2 NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'CancellationDecisionBy') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD CancellationDecisionBy bigint NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'CancellationDecisionAt') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD CancellationDecisionAt datetime2 NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'CancellationDecisionNote') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD CancellationDecisionNote nvarchar(500) NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'SupportRequestedBy') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD SupportRequestedBy bigint NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'SupportRequestedAt') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD SupportRequestedAt datetime2 NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'SupportRequestNote') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD SupportRequestNote nvarchar(500) NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'EventType') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD EventType varchar(40) NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'OldStatus') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD OldStatus varchar(20) NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'Note') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD Note nvarchar(1000) NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'CreatedBy') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD CreatedBy bigint NULL;
IF COL_LENGTH('dbo.DiagnosisAssignments', 'CreatedAt') IS NULL
    ALTER TABLE dbo.DiagnosisAssignments ADD CreatedAt datetime2 NULL;

UPDATE dbo.DiagnosisAssignments
SET EventType = ISNULL(NULLIF(EventType, ''), 'SNAPSHOT')
WHERE EventType IS NULL OR LTRIM(RTRIM(EventType)) = '';

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_DiagnosisAssignments_StatusTo_CreatedAt'
      AND object_id = OBJECT_ID('dbo.DiagnosisAssignments')
)
BEGIN
    DROP INDEX IX_DiagnosisAssignments_StatusTo_CreatedAt ON dbo.DiagnosisAssignments;
END;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_DiagnosisAssignments_NewStatus_CreatedAt'
      AND object_id = OBJECT_ID('dbo.DiagnosisAssignments')
)
BEGIN
    DROP INDEX IX_DiagnosisAssignments_NewStatus_CreatedAt ON dbo.DiagnosisAssignments;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_DiagnosisAssignments_Status_CreatedAt'
      AND object_id = OBJECT_ID('dbo.DiagnosisAssignments')
)
   AND COL_LENGTH('dbo.DiagnosisAssignments', 'Status') IS NOT NULL
BEGIN
    CREATE INDEX IX_DiagnosisAssignments_Status_CreatedAt
        ON dbo.DiagnosisAssignments (Status, CreatedAt DESC);
END;

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_DiagnosisAssignments_Diagnoses'
      AND parent_object_id = OBJECT_ID('dbo.DiagnosisAssignments')
)
BEGIN
    ALTER TABLE dbo.DiagnosisAssignments DROP CONSTRAINT FK_DiagnosisAssignments_Diagnoses;
END;

ALTER TABLE dbo.DiagnosisAssignments
ADD CONSTRAINT FK_DiagnosisAssignments_Diagnoses
    FOREIGN KEY (DiagnosisId) REFERENCES dbo.Diagnoses(Id)
    ON DELETE CASCADE;


