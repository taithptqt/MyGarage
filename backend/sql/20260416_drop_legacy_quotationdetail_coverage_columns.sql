SET XACT_ABORT ON;
BEGIN TRAN;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_QuotationDetails_CoverageSum')
BEGIN
    ALTER TABLE dbo.QuotationDetails DROP CONSTRAINT CK_QuotationDetails_CoverageSum;
END

IF COL_LENGTH('dbo.QuotationDetails', 'InsuranceRequestedAmount') IS NOT NULL
BEGIN
    ALTER TABLE dbo.QuotationDetails DROP COLUMN InsuranceRequestedAmount;
END

IF COL_LENGTH('dbo.QuotationDetails', 'CustomerCoPayAmount') IS NOT NULL
BEGIN
    ALTER TABLE dbo.QuotationDetails DROP COLUMN CustomerCoPayAmount;
END

ALTER TABLE dbo.QuotationDetails
ADD CONSTRAINT CK_QuotationDetails_CoverageSum CHECK (
    ABS(
        ISNULL([InsuranceCoveredAmount], 0) +
        ISNULL([CustomerCoveredAmount], 0) -
        ISNULL([FinalAmount], 0)
    ) <= 0.01
);

COMMIT TRAN;
