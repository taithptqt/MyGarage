/*
  Diagnosis upgrade (2026-04-13)
  1) Create DiagnosisAssignments table (event history for diagnose workflow)
  2) Normalize diagnosis status values to uppercase canonical values:
     PENDING, ASSIGNED, COMPLETED, CANCELED
  3) Normalize CANCELLED -> CANCELED in diagnosis domain fields
  4) Backfill one snapshot log row for existing Diagnoses (if missing)
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.DiagnosisAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DiagnosisAssignments
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiagnosisAssignments PRIMARY KEY,
        DiagnosisId bigint NOT NULL,
        AssignedTeamId bigint NULL,
        TechnicianId bigint NULL,
        AssistantTechnicianId bigint NULL,
        AssignedBayId bigint NULL,
        Status varchar(20) NULL,
        AssignedAt datetime2 NULL,
        TechnicianAcceptedAt datetime2 NULL,
        TechnicianCompletedAt datetime2 NULL,
        ApprovedByTeamLeaderAt datetime2 NULL,
        TeamLeaderNote nvarchar(500) NULL,
        ApprovedByForemanId bigint NULL,
        ApprovedByForemanAt datetime2 NULL,
        ForemanNote nvarchar(500) NULL,
        CancellationStatus varchar(20) NULL,
        CancellationReason nvarchar(500) NULL,
        CancellationRequestedBy bigint NULL,
        CancellationRequestedAt datetime2 NULL,
        CancellationDecisionBy bigint NULL,
        CancellationDecisionAt datetime2 NULL,
        CancellationDecisionNote nvarchar(500) NULL,
        SupportRequestedBy bigint NULL,
        SupportRequestedAt datetime2 NULL,
        SupportRequestNote nvarchar(500) NULL,
        EventType varchar(40) NOT NULL,
        OldStatus varchar(20) NULL,
        Note nvarchar(1000) NULL,
        CreatedBy bigint NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_DiagnosisAssignments_CreatedAt DEFAULT (getdate())
    );

    ALTER TABLE dbo.DiagnosisAssignments
    ADD CONSTRAINT FK_DiagnosisAssignments_Diagnoses
        FOREIGN KEY (DiagnosisId) REFERENCES dbo.Diagnoses(Id)
        ON DELETE CASCADE;

    CREATE INDEX IX_DiagnosisAssignments_DiagnosisId_CreatedAt
        ON dbo.DiagnosisAssignments (DiagnosisId, CreatedAt DESC);

    CREATE INDEX IX_DiagnosisAssignments_Status_CreatedAt
        ON dbo.DiagnosisAssignments (Status, CreatedAt DESC);
END;

UPDATE dbo.Diagnoses
SET Status = CASE UPPER(LTRIM(RTRIM(ISNULL(Status, ''))))
    WHEN 'PENDING' THEN 'PENDING'
    WHEN 'ASSIGNED' THEN 'ASSIGNED'
    WHEN 'COMPLETED' THEN 'COMPLETED'
    WHEN 'CANCELLED' THEN 'CANCELED'
    WHEN 'CANCELED' THEN 'CANCELED'
    ELSE Status
END
WHERE Status IS NOT NULL;

UPDATE dbo.Diagnoses
SET CancellationStatus = CASE UPPER(LTRIM(RTRIM(ISNULL(CancellationStatus, ''))))
    WHEN '' THEN 'NONE'
    WHEN 'CANCELLED' THEN 'CANCELED'
    WHEN 'CANCELED' THEN 'CANCELED'
    WHEN 'NONE' THEN 'NONE'
    WHEN 'REQUESTED' THEN 'REQUESTED'
    WHEN 'APPROVED' THEN 'APPROVED'
    WHEN 'REJECTED' THEN 'REJECTED'
    ELSE CancellationStatus
END
WHERE CancellationStatus IS NOT NULL;

INSERT INTO dbo.DiagnosisAssignments
(
    DiagnosisId,
    AssignedTeamId,
    TechnicianId,
    AssistantTechnicianId,
    AssignedBayId,
    Status,
    AssignedAt,
    TechnicianAcceptedAt,
    TechnicianCompletedAt,
    ApprovedByTeamLeaderAt,
    TeamLeaderNote,
    ApprovedByForemanId,
    ApprovedByForemanAt,
    ForemanNote,
    CancellationStatus,
    CancellationReason,
    CancellationRequestedBy,
    CancellationRequestedAt,
    CancellationDecisionBy,
    CancellationDecisionAt,
    CancellationDecisionNote,
    SupportRequestedBy,
    SupportRequestedAt,
    SupportRequestNote,
    EventType,
    OldStatus,
    Note,
    CreatedBy,
    CreatedAt
)
SELECT
    d.Id,
    d.AssignedTeamId,
    d.TechnicianId,
    d.AssistantTechnicianId,
    d.AssignedBayId,
    d.Status,
    d.AssignedAt,
    d.TechnicianAcceptedAt,
    d.TechnicianCompletedAt,
    d.ApprovedByTeamLeaderAt,
    d.TeamLeaderNote,
    d.ApprovedByForemanId,
    d.ApprovedByForemanAt,
    d.ForemanNote,
    d.CancellationStatus,
    d.CancellationReason,
    d.CancellationRequestedBy,
    d.CancellationRequestedAt,
    d.CancellationDecisionBy,
    d.CancellationDecisionAt,
    d.CancellationDecisionNote,
    d.SupportRequestedBy,
    d.SupportRequestedAt,
    d.SupportRequestNote,
    'SNAPSHOT',
    NULL,
    'Backfilled from Diagnoses table',
    d.UpdatedBy,
    COALESCE(d.UpdatedAt, d.CreatedAt, GETDATE())
FROM dbo.Diagnoses d
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.DiagnosisAssignments l
    WHERE l.DiagnosisId = d.Id
);


