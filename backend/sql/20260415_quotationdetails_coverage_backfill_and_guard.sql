SET XACT_ABORT ON;
BEGIN TRANSACTION;

;WITH base_data AS (
    SELECT
        qd.Id,
        CAST(ISNULL(qd.FinalAmount, 0) AS DECIMAL(18,2)) AS FinalAmount,
        UPPER(LTRIM(RTRIM(ISNULL(qd.PayerType, 'CUSTOMER')))) AS PayerTypeNorm,
        CAST(COALESCE(qd.InsuranceCoveredAmount,
            CASE WHEN UPPER(LTRIM(RTRIM(ISNULL(qd.PayerType, '')))) = 'INSURANCE' THEN ISNULL(qd.FinalAmount,0) ELSE 0 END
        ) AS DECIMAL(18,2)) AS InsuranceSeed,
        CAST(COALESCE(qd.CustomerCoveredAmount,
            CASE WHEN UPPER(LTRIM(RTRIM(ISNULL(qd.PayerType, '')))) = 'CUSTOMER' THEN ISNULL(qd.FinalAmount,0) ELSE 0 END
        ) AS DECIMAL(18,2)) AS CustomerSeed
    FROM dbo.QuotationDetails qd
), clamped AS (
    SELECT
        b.Id,
        b.FinalAmount,
        b.PayerTypeNorm,
        CAST(CASE WHEN b.InsuranceSeed < 0 THEN 0 WHEN b.InsuranceSeed > b.FinalAmount THEN b.FinalAmount ELSE b.InsuranceSeed END AS DECIMAL(18,2)) AS InsuranceClamped,
        CAST(CASE WHEN b.CustomerSeed < 0 THEN 0 WHEN b.CustomerSeed > b.FinalAmount THEN b.FinalAmount ELSE b.CustomerSeed END AS DECIMAL(18,2)) AS CustomerClamped
    FROM base_data b
), resolved AS (
    SELECT
        c.Id,
        c.FinalAmount,
        CAST(
            CASE
                WHEN c.PayerTypeNorm = 'INSURANCE' THEN c.FinalAmount
                WHEN c.PayerTypeNorm = 'CUSTOMER' THEN 0
                WHEN c.InsuranceClamped + c.CustomerClamped = c.FinalAmount THEN c.InsuranceClamped
                WHEN c.InsuranceClamped BETWEEN 0 AND c.FinalAmount THEN c.InsuranceClamped
                WHEN c.CustomerClamped BETWEEN 0 AND c.FinalAmount THEN c.FinalAmount - c.CustomerClamped
                ELSE 0
            END
        AS DECIMAL(18,2)) AS InsuranceResolved
    FROM clamped c
)
UPDATE qd
SET
    InsuranceCoveredAmount = r.InsuranceResolved,
    CustomerCoveredAmount = CAST(r.FinalAmount - r.InsuranceResolved AS DECIMAL(18,2)),
    PayerType = CASE
        WHEN r.InsuranceResolved = r.FinalAmount THEN 'INSURANCE'
        WHEN r.InsuranceResolved = 0 THEN 'CUSTOMER'
        ELSE 'SPLIT'
    END
FROM dbo.QuotationDetails qd
JOIN resolved r ON r.Id = qd.Id;

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.QuotationDetails')
      AND name = 'CK_QuotationDetails_CoverageSum'
)
BEGIN
    ALTER TABLE dbo.QuotationDetails DROP CONSTRAINT CK_QuotationDetails_CoverageSum;
END;

ALTER TABLE dbo.QuotationDetails WITH CHECK
ADD CONSTRAINT CK_QuotationDetails_CoverageSum CHECK (
    ABS(
        ISNULL([InsuranceCoveredAmount], 0) +
        ISNULL([CustomerCoveredAmount], 0) -
        ISNULL([FinalAmount], 0)
    ) <= 0.01
);

COMMIT TRANSACTION;
