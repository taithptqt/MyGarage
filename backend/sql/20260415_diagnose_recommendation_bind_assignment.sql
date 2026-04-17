SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.DiagnosisRecommendations', 'DiagnosisAssignmentId') IS NULL
BEGIN
    ALTER TABLE dbo.DiagnosisRecommendations
        ADD DiagnosisAssignmentId BIGINT NULL;
END;

;WITH MissingAssignmentDiagnoses AS
(
    SELECT DISTINCT dr.DiagnoseId
    FROM dbo.DiagnosisRecommendations dr
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
FROM dbo.DiagnosisRecommendations dr
JOIN LatestAssignment la
    ON la.DiagnosisId = dr.DiagnoseId
   AND la.rn = 1
WHERE dr.DiagnosisAssignmentId IS NULL;

IF EXISTS (
    SELECT 1
    FROM dbo.DiagnosisRecommendations
    WHERE DiagnosisAssignmentId IS NULL
)
BEGIN
    THROW 50001, ''Migration failed: Some DiagnosisRecommendations still have NULL DiagnosisAssignmentId.'', 1;
END;

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = ''FK_DiagnosisRecommendations_DiagnosisAssignments''
      AND parent_object_id = OBJECT_ID(''dbo.DiagnosisRecommendations'')
)
BEGIN
    ALTER TABLE dbo.DiagnosisRecommendations DROP CONSTRAINT FK_DiagnosisRecommendations_DiagnosisAssignments;
END;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(''dbo.DiagnosisRecommendations'')
      AND name = ''IX_DiagnosisRecommendations_DiagnosisAssignmentId''
)
BEGIN
    DROP INDEX IX_DiagnosisRecommendations_DiagnosisAssignmentId ON dbo.DiagnosisRecommendations;
END;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(''dbo.DiagnosisRecommendations'')
      AND name = ''DiagnosisAssignmentId''
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.DiagnosisRecommendations
        ALTER COLUMN DiagnosisAssignmentId BIGINT NOT NULL;
END;

CREATE INDEX IX_DiagnosisRecommendations_DiagnosisAssignmentId
    ON dbo.DiagnosisRecommendations (DiagnosisAssignmentId);

ALTER TABLE dbo.DiagnosisRecommendations
    WITH CHECK ADD CONSTRAINT FK_DiagnosisRecommendations_DiagnosisAssignments
        FOREIGN KEY (DiagnosisAssignmentId)
        REFERENCES dbo.DiagnosisAssignments (Id);
');

COMMIT TRANSACTION;
