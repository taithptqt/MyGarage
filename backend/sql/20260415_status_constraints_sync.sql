SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1) Quotations.Status: sync with insurance/split-billing workflow
UPDATE dbo.Quotations
SET [Status] = 'CANCELED'
WHERE UPPER(LTRIM(RTRIM(ISNULL([Status], '')))) = 'CANCELLED';

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.Quotations')
      AND name = 'CK_Quotations_Status'
)
BEGIN
    ALTER TABLE dbo.Quotations DROP CONSTRAINT CK_Quotations_Status;
END;

ALTER TABLE dbo.Quotations
    ADD CONSTRAINT CK_Quotations_Status CHECK (
        [Status] IN (
            'DRAFT',
            'PENDING_INSURANCE',
            'INSURANCE_APPROVED',
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

-- 2) QuotationDetails: ensure split-billing columns exist
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

-- 3) QuotationDetails.PayerType enum
UPDATE dbo.QuotationDetails
SET PayerType = CASE
    WHEN UPPER(LTRIM(RTRIM(ISNULL(PayerType, '')))) IN ('INSURANCE', 'CUSTOMER', 'SPLIT')
        THEN UPPER(LTRIM(RTRIM(PayerType)))
    WHEN ISNULL(InsuranceCoveredAmount, 0) > 0 AND ISNULL(CustomerCoveredAmount, 0) > 0
        THEN 'SPLIT'
    WHEN ISNULL(InsuranceCoveredAmount, 0) > 0
        THEN 'INSURANCE'
    ELSE 'CUSTOMER'
END;

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.QuotationDetails')
      AND name = 'CK_QuotationDetails_PayerType'
)
BEGIN
    ALTER TABLE dbo.QuotationDetails DROP CONSTRAINT CK_QuotationDetails_PayerType;
END;

ALTER TABLE dbo.QuotationDetails
    ADD CONSTRAINT CK_QuotationDetails_PayerType CHECK (
        [PayerType] IN ('INSURANCE', 'CUSTOMER', 'SPLIT')
    );

-- 4) DiagnosisAssignments approval enums
UPDATE dbo.DiagnosisAssignments
SET TeamLeaderApprovalStatus = NULL
WHERE TeamLeaderApprovalStatus IS NOT NULL
  AND UPPER(LTRIM(RTRIM(TeamLeaderApprovalStatus))) NOT IN ('PENDING', 'APPROVED', 'REJECTED', 'BYPASSED', 'ESCALATED');

UPDATE dbo.DiagnosisAssignments
SET FinalApprovalStatus = NULL
WHERE FinalApprovalStatus IS NOT NULL
  AND UPPER(LTRIM(RTRIM(FinalApprovalStatus))) NOT IN ('PENDING', 'APPROVED', 'REJECTED');

UPDATE dbo.DiagnosisAssignments
SET TeamLeaderApprovalStatus = UPPER(LTRIM(RTRIM(TeamLeaderApprovalStatus)))
WHERE TeamLeaderApprovalStatus IS NOT NULL;

UPDATE dbo.DiagnosisAssignments
SET FinalApprovalStatus = UPPER(LTRIM(RTRIM(FinalApprovalStatus)))
WHERE FinalApprovalStatus IS NOT NULL;

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.DiagnosisAssignments')
      AND name = 'CK_DiagnosisAssignments_TeamLeaderApprovalStatus'
)
BEGIN
    ALTER TABLE dbo.DiagnosisAssignments DROP CONSTRAINT CK_DiagnosisAssignments_TeamLeaderApprovalStatus;
END;

ALTER TABLE dbo.DiagnosisAssignments
    ADD CONSTRAINT CK_DiagnosisAssignments_TeamLeaderApprovalStatus CHECK (
        [TeamLeaderApprovalStatus] IS NULL OR
        [TeamLeaderApprovalStatus] IN ('PENDING', 'APPROVED', 'REJECTED', 'BYPASSED', 'ESCALATED')
    );

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.DiagnosisAssignments')
      AND name = 'CK_DiagnosisAssignments_FinalApprovalStatus'
)
BEGIN
    ALTER TABLE dbo.DiagnosisAssignments DROP CONSTRAINT CK_DiagnosisAssignments_FinalApprovalStatus;
END;

ALTER TABLE dbo.DiagnosisAssignments
    ADD CONSTRAINT CK_DiagnosisAssignments_FinalApprovalStatus CHECK (
        [FinalApprovalStatus] IS NULL OR
        [FinalApprovalStatus] IN ('PENDING', 'APPROVED', 'REJECTED')
    );

COMMIT TRANSACTION;
