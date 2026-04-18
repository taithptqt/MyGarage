/* Remap legacy quotations that still miss DiagnosisAssignmentId. */

;WITH LatestAssignmentByServiceOrder AS (
    SELECT
        d.ServiceOrderId,
        da.Id AS DiagnosisAssignmentId,
        da.DiagnosisId,
        ROW_NUMBER() OVER (
            PARTITION BY d.ServiceOrderId
            ORDER BY da.CreatedAt DESC, da.Id DESC
        ) AS RowNum
    FROM dbo.DiagnosisAssignments da
    INNER JOIN dbo.Diagnoses d ON d.Id = da.DiagnosisId
)
UPDATE q
SET
    q.DiagnosisAssignmentId = la.DiagnosisAssignmentId,
    q.DiagnosisId = COALESCE(q.DiagnosisId, la.DiagnosisId)
FROM dbo.Quotations q
INNER JOIN LatestAssignmentByServiceOrder la
    ON la.ServiceOrderId = q.ServiceOrderId
   AND la.RowNum = 1
WHERE q.DiagnosisAssignmentId IS NULL;

SELECT @@ROWCOUNT AS UpdatedRows;
