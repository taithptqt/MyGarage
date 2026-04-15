SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1) Quotations: insurance claim header + workflow status for insurance flow
IF COL_LENGTH('dbo.Quotations', 'InsuranceClaimNumber') IS NULL
BEGIN
    ALTER TABLE dbo.Quotations
        ADD InsuranceClaimNumber NVARCHAR(100) NULL;
END;

IF COL_LENGTH('dbo.Quotations', 'TotalInsuranceAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Quotations
        ADD TotalInsuranceAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Quotations_TotalInsuranceAmount DEFAULT (0);
END;

IF COL_LENGTH('dbo.Quotations', 'TotalCustomerAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Quotations
        ADD TotalCustomerAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Quotations_TotalCustomerAmount DEFAULT (0);
END;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Quotations_Status')
BEGIN
    ALTER TABLE dbo.Quotations DROP CONSTRAINT CK_Quotations_Status;
END;

UPDATE dbo.Quotations
SET [Status] = 'CANCELED'
WHERE UPPER(LTRIM(RTRIM(ISNULL([Status], '')))) = 'CANCELLED';

ALTER TABLE dbo.Quotations
    ADD CONSTRAINT CK_Quotations_Status CHECK (
        [Status] IN (
            'DRAFT',
            'PENDING_INSURANCE',
            'PENDING_CUSTOMER',
            'WAITING_APPROVAL',
            'SENT_TO_CUSTOMER',
            'APPROVED',
            'CUSTOMER_APPROVED',
            'CUSTOMER_REJECTED',
            'REJECTED',
            'CANCELED',
            'SUPERSEDED'
        )
    );

-- 2) QuotationDetails: split billing coverage columns + enum + arithmetic guard
IF COL_LENGTH('dbo.QuotationDetails', 'InsuranceCoveredAmount') IS NULL
BEGIN
    ALTER TABLE dbo.QuotationDetails
        ADD InsuranceCoveredAmount DECIMAL(18,2) NULL;
END;

IF COL_LENGTH('dbo.QuotationDetails', 'CustomerCoveredAmount') IS NULL
BEGIN
    ALTER TABLE dbo.QuotationDetails
        ADD CustomerCoveredAmount DECIMAL(18,2) NULL;
END;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_QuotationDetails_PayerType')
BEGIN
    ALTER TABLE dbo.QuotationDetails DROP CONSTRAINT CK_QuotationDetails_PayerType;
END;

ALTER TABLE dbo.QuotationDetails
    ADD CONSTRAINT CK_QuotationDetails_PayerType CHECK (
        [PayerType] IN ('INSURANCE', 'CUSTOMER', 'SPLIT')
    );

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_QuotationDetails_CoverageSum')
BEGIN
    ALTER TABLE dbo.QuotationDetails DROP CONSTRAINT CK_QuotationDetails_CoverageSum;
END;

IF COL_LENGTH('dbo.QuotationDetails', 'InsuranceCoveredAmount') IS NOT NULL
   AND COL_LENGTH('dbo.QuotationDetails', 'CustomerCoveredAmount') IS NOT NULL
BEGIN
    EXEC('
        ALTER TABLE dbo.QuotationDetails
            ADD CONSTRAINT CK_QuotationDetails_CoverageSum CHECK (
                (
                    ISNULL([InsuranceCoveredAmount], ISNULL([InsuranceRequestedAmount], 0)) +
                    ISNULL([CustomerCoveredAmount], ISNULL([CustomerCoPayAmount], 0))
                ) = ISNULL([FinalAmount], 0)
            );
    ');
END;

-- 3) DiagnosisAssignments: insurance proof gate fields
IF COL_LENGTH('dbo.DiagnosisAssignments', 'RequiresInsuranceProof') IS NULL
BEGIN
    ALTER TABLE dbo.DiagnosisAssignments
        ADD RequiresInsuranceProof BIT NOT NULL CONSTRAINT DF_DiagnosisAssignments_RequiresInsuranceProof DEFAULT (0);
END;

IF COL_LENGTH('dbo.DiagnosisAssignments', 'ProofImageUrl') IS NULL
BEGIN
    ALTER TABLE dbo.DiagnosisAssignments
        ADD ProofImageUrl NVARCHAR(500) NULL;
END;

-- 3.1) ServiceStaffAssignments (work order tasks): insurance proof gate fields
IF COL_LENGTH('dbo.ServiceStaffAssignments', 'RequiresInsuranceProof') IS NULL
BEGIN
    ALTER TABLE dbo.ServiceStaffAssignments
        ADD RequiresInsuranceProof BIT NOT NULL CONSTRAINT DF_ServiceStaffAssignments_RequiresInsuranceProof DEFAULT (0);
END;

IF COL_LENGTH('dbo.ServiceStaffAssignments', 'ProofImageUrl') IS NULL
BEGIN
    ALTER TABLE dbo.ServiceStaffAssignments
        ADD ProofImageUrl NVARCHAR(500) NULL;
END;

-- 4) Diagnosis status check compatibility (avoid CK_Status_Enum conflicts after workflow normalization)
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Status_Enum' AND parent_object_id = OBJECT_ID('dbo.Diagnoses'))
BEGIN
    ALTER TABLE dbo.Diagnoses DROP CONSTRAINT CK_Status_Enum;
END;

ALTER TABLE dbo.Diagnoses
    ADD CONSTRAINT CK_Status_Enum CHECK (
        [Status] IN ('PENDING', 'ASSIGNED', 'COMPLETED', 'CANCELED')
    );

COMMIT TRANSACTION;
