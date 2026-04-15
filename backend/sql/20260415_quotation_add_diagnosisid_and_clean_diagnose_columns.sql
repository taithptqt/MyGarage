SET XACT_ABORT ON;
BEGIN TRAN;

IF COL_LENGTH('dbo.DiagnoseRecommendations', 'DiagnoseId') IS NOT NULL
   AND COL_LENGTH('dbo.DiagnoseRecommendations', 'DiagnosisId') IS NULL
BEGIN
    EXEC sp_rename 'dbo.DiagnoseRecommendations.DiagnoseId', 'DiagnosisId', 'COLUMN';
END;

IF COL_LENGTH('dbo.QuotationDetails', 'DiagnoseRecommendationId') IS NOT NULL
   AND COL_LENGTH('dbo.QuotationDetails', 'DiagnosisRecommendationId') IS NULL
BEGIN
    EXEC sp_rename 'dbo.QuotationDetails.DiagnoseRecommendationId', 'DiagnosisRecommendationId', 'COLUMN';
END;

IF COL_LENGTH('dbo.ServiceOrderDetails', 'DiagnoseRecommendationId') IS NOT NULL
   AND COL_LENGTH('dbo.ServiceOrderDetails', 'DiagnosisRecommendationId') IS NULL
BEGIN
    EXEC sp_rename 'dbo.ServiceOrderDetails.DiagnoseRecommendationId', 'DiagnosisRecommendationId', 'COLUMN';
END;

IF COL_LENGTH('dbo.TechnicalProposalSnapshotDetails', 'DiagnoseRecommendationId') IS NOT NULL
   AND COL_LENGTH('dbo.TechnicalProposalSnapshotDetails', 'DiagnosisRecommendationId') IS NULL
BEGIN
    EXEC sp_rename 'dbo.TechnicalProposalSnapshotDetails.DiagnoseRecommendationId', 'DiagnosisRecommendationId', 'COLUMN';
END;

IF COL_LENGTH('dbo.Quotations', 'DiagnosisId') IS NULL
BEGIN
    ALTER TABLE dbo.Quotations ADD DiagnosisId BIGINT NULL;
END;

IF COL_LENGTH('dbo.Quotations', 'DiagnosisId') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE q
        SET q.DiagnosisId = tps.DiagnosisId
        FROM dbo.Quotations q
        JOIN dbo.TechnicalProposalSnapshots tps ON tps.Id = q.SourceSnapshotId
        WHERE q.DiagnosisId IS NULL
          AND tps.DiagnosisId IS NOT NULL;
    ');

    IF COL_LENGTH('dbo.QuotationDetails', 'DiagnosisRecommendationId') IS NOT NULL
       AND COL_LENGTH('dbo.DiagnoseRecommendations', 'DiagnosisId') IS NOT NULL
    BEGIN
        EXEC(N'
            ;WITH qd_pick AS (
                SELECT qd.QuotationId, MIN(qd.DiagnosisRecommendationId) AS DiagnosisRecommendationId
                FROM dbo.QuotationDetails qd
                WHERE qd.DiagnosisRecommendationId IS NOT NULL
                GROUP BY qd.QuotationId
            )
            UPDATE q
            SET q.DiagnosisId = dr.DiagnosisId
            FROM dbo.Quotations q
            JOIN qd_pick pick ON pick.QuotationId = q.Id
            JOIN dbo.DiagnoseRecommendations dr ON dr.Id = pick.DiagnosisRecommendationId
            WHERE q.DiagnosisId IS NULL
              AND dr.DiagnosisId IS NOT NULL;
        ');
    END;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Quotations')
      AND name = 'IX_Quotations_DiagnosisId'
)
BEGIN
    EXEC(N'CREATE INDEX IX_Quotations_DiagnosisId ON dbo.Quotations (DiagnosisId);');
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_Quotations_Diagnoses'
)
BEGIN
    EXEC(N'
        ALTER TABLE dbo.Quotations
        ADD CONSTRAINT FK_Quotations_Diagnoses
        FOREIGN KEY (DiagnosisId)
        REFERENCES dbo.Diagnoses (Id);
    ');
END;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.QuotationDetails')
      AND name = 'IX_QuotationDetails_ServiceOrderId_DiagnoseRecommendationId'
)
BEGIN
    EXEC sp_rename
        'dbo.QuotationDetails.IX_QuotationDetails_ServiceOrderId_DiagnoseRecommendationId',
        'IX_QuotationDetails_ServiceOrderId_DiagnosisRecommendationId',
        'INDEX';
END;

COMMIT TRAN;
PRINT 'DB_MIGRATION_OK';
