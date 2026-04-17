SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1) Rename legacy table DiagnoseRecommendations -> DiagnosisRecommendations
IF OBJECT_ID(N'dbo.DiagnoseRecommendations', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.DiagnosisRecommendations', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.DiagnoseRecommendations', N'DiagnosisRecommendations';
END

-- 2) Normalize old FK name from QuotationDetails to renamed table (optional)
IF OBJECT_ID(N'dbo.FK_QuotationDetails_DiagnoseRecommendations', N'F') IS NOT NULL
BEGIN
    EXEC sp_rename
        N'dbo.FK_QuotationDetails_DiagnoseRecommendations',
        N'FK_QuotationDetails_DiagnosisRecommendations',
        N'OBJECT';
END

-- 3) Remove direct Recommendation link from ServiceOrderDetails.
--    Business flow: DiagnosisRecommendation -> QuotationDetail -> ServiceOrderDetail
IF COL_LENGTH('dbo.ServiceOrderDetails', 'DiagnosisRecommendationId') IS NOT NULL
BEGIN
    DECLARE @fkName NVARCHAR(128);

    SELECT TOP 1 @fkName = fk.name
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = 'ServiceOrderDetails'
      AND c.name = 'DiagnosisRecommendationId';

    IF @fkName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE dbo.ServiceOrderDetails DROP CONSTRAINT [' + @fkName + ']');
    END

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ServiceOrderDetails')
          AND name = N'IX_ServiceOrderDetails_DiagnosisRecommendationId'
    )
    BEGIN
        DROP INDEX [IX_ServiceOrderDetails_DiagnosisRecommendationId] ON [dbo].[ServiceOrderDetails];
    END

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ServiceOrderDetails')
          AND name = N'IX_ServiceOrderDetails_DiagnoseRecommendationId'
    )
    BEGIN
        DROP INDEX [IX_ServiceOrderDetails_DiagnoseRecommendationId] ON [dbo].[ServiceOrderDetails];
    END

    ALTER TABLE dbo.ServiceOrderDetails DROP COLUMN DiagnosisRecommendationId;
END

COMMIT TRANSACTION;

