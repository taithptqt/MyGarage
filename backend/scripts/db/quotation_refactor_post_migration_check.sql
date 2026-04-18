/* Post-migration checks for quotation header-to-header refactor. */

SELECT
    'Column_QuotationDetails_DiagnosisRecommendationId' AS CheckName,
    COUNT(1) AS CheckValue
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'QuotationDetails'
  AND COLUMN_NAME = 'DiagnosisRecommendationId';

SELECT
    'Column_Quotations_DiagnosisAssignmentId' AS CheckName,
    COUNT(1) AS CheckValue
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Quotations'
  AND COLUMN_NAME = 'DiagnosisAssignmentId';

SELECT
    'FK_Quotations_DiagnosisAssignments_Exists' AS CheckName,
    COUNT(1) AS CheckValue
FROM sys.foreign_keys
WHERE name = 'FK_Quotations_DiagnosisAssignments';

SELECT
    'IX_Quotations_DiagnosisAssignmentId_Exists' AS CheckName,
    COUNT(1) AS CheckValue
FROM sys.indexes
WHERE name = 'IX_Quotations_DiagnosisAssignmentId'
  AND object_id = OBJECT_ID(N'dbo.Quotations');

SELECT
    'Quotations_Missing_DiagnosisAssignmentId' AS CheckName,
    COUNT(1) AS CheckValue
FROM dbo.Quotations
WHERE DiagnosisAssignmentId IS NULL;

SELECT
    'Quotations_With_DiagnosisId_Null' AS CheckName,
    COUNT(1) AS CheckValue
FROM dbo.Quotations
WHERE DiagnosisId IS NULL;

SELECT
    'Quotations_DiagnosisAssignment_Diagnosis_Mismatch' AS CheckName,
    COUNT(1) AS CheckValue
FROM dbo.Quotations q
INNER JOIN dbo.DiagnosisAssignments da ON da.Id = q.DiagnosisAssignmentId
WHERE q.DiagnosisId IS NOT NULL
  AND q.DiagnosisId <> da.DiagnosisId;

SELECT
    q.Id,
    q.Code,
    q.ServiceOrderId,
    q.VersionNo,
    q.DiagnosisId,
    q.DiagnosisAssignmentId,
    q.CreatedAt
FROM dbo.Quotations q
WHERE q.DiagnosisAssignmentId IS NULL
ORDER BY q.ServiceOrderId, q.VersionNo, q.Id;
