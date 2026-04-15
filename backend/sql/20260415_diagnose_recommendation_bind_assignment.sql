SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.DiagnoseRecommendations', 'DiagnosisAssignmentId') IS NULL
BEGIN
    ALTER TABLE dbo.DiagnoseRecommendations
        ADD DiagnosisAssignmentId BIGINT NULL;
END;

;WITH MissingAssignmentDiagnoses AS
(
    SELECT DISTINCT dr.DiagnoseId
    FROM dbo.DiagnoseRecommendations dr
    LEFT JOIN dbo.DiagnosisAssignments da ON da.DiagnosisId = dr.DiagnoseId
    WHERE da.Id IS NULL
)
INSERT INTO dbo.DiagnosisAssignments
(
    DiagnosisId,
    Status,
    EventType,
    CreatedAt
)
SELECT
    mad.DiagnoseId,
    d.Status,
    'MIGRATION_BACKFILL',
    GETDATE()
FROM MissingAssignmentDiagnoses mad
JOIN dbo.Diagnoses d ON d.Id = mad.DiagnoseId;

EXEC(N'
;WITH LatestAssignment AS
(
    SELECT
        da.DiagnosisId,
        da.Id,
        ROW_NUMBER() OVER (PARTITION BY da.DiagnosisId ORDER BY da.CreatedAt DESC, da.Id DESC) AS rn
    FROM dbo.DiagnosisAssignments da
)
UPDATE dr
SET dr.DiagnosisAssignmentId = la.Id
FROM dbo.DiagnoseRecommendations dr
JOIN LatestAssignment la
    ON la.DiagnosisId = dr.DiagnoseId
   AND la.rn = 1
WHERE dr.DiagnosisAssignmentId IS NULL;

IF EXISTS (
    SELECT 1
    FROM dbo.DiagnoseRecommendations
    WHERE DiagnosisAssignmentId IS NULL
)
BEGIN
    THROW 50001, ''Migration failed: Some DiagnoseRecommendations still have NULL DiagnosisAssignmentId.'', 1;
END;

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = ''FK_DiagnoseRecommendations_DiagnosisAssignments''
      AND parent_object_id = OBJECT_ID(''dbo.DiagnoseRecommendations'')
)
BEGIN
    ALTER TABLE dbo.DiagnoseRecommendations DROP CONSTRAINT FK_DiagnoseRecommendations_DiagnosisAssignments;
END;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(''dbo.DiagnoseRecommendations'')
      AND name = ''IX_DiagnoseRecommendations_DiagnosisAssignmentId''
)
BEGIN
    DROP INDEX IX_DiagnoseRecommendations_DiagnosisAssignmentId ON dbo.DiagnoseRecommendations;
END;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(''dbo.DiagnoseRecommendations'')
      AND name = ''DiagnosisAssignmentId''
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.DiagnoseRecommendations
        ALTER COLUMN DiagnosisAssignmentId BIGINT NOT NULL;
END;

CREATE INDEX IX_DiagnoseRecommendations_DiagnosisAssignmentId
    ON dbo.DiagnoseRecommendations (DiagnosisAssignmentId);

ALTER TABLE dbo.DiagnoseRecommendations
    WITH CHECK ADD CONSTRAINT FK_DiagnoseRecommendations_DiagnosisAssignments
        FOREIGN KEY (DiagnosisAssignmentId)
        REFERENCES dbo.DiagnosisAssignments (Id);
');

COMMIT TRANSACTION;
