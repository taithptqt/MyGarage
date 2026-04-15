SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
Seed du lieu demo E2E cho luong:
Pending_Insurance -> Insurance_Approved -> Pending_Customer -> Customer_Approved

Cach dung:
1) Chay script nay tren DB MyGarage.
2) Script se tao 1 quotation demo dang PENDING_CUSTOMER + magic link ACTIVE.
3) Lay ket qua SELECT cuoi script de test frontend/public portal.
*/

DECLARE @Now DATETIME2 = GETDATE();
DECLARE @ServiceOrderId BIGINT;
DECLARE @ActorStaffId BIGINT;
DECLARE @CustomerEmail NVARCHAR(255);
DECLARE @Plate NVARCHAR(50);
DECLARE @BranchId BIGINT;
DECLARE @QuotationId BIGINT;
DECLARE @RawToken VARCHAR(120);
DECLARE @TokenHash VARCHAR(64);
DECLARE @PortalBase NVARCHAR(255) = N'http://localhost:5173/quote-access';
DECLARE @Code VARCHAR(64);
DECLARE @FinalAmount DECIMAL(18,2) = 40100000;
DECLARE @InsuranceAmount DECIMAL(18,2) = 30000000;
DECLARE @CustomerAmount DECIMAL(18,2) = 10100000;

SELECT TOP 1
    @ServiceOrderId = so.Id,
    @ActorStaffId = COALESCE(so.ServiceAdvisorId, so.ReceptionistId, so.UpdatedBy, so.CreatedBy),
    @BranchId = so.ExecuteBranchId,
    @CustomerEmail = c.Email,
    @Plate = v.LicensePlate
FROM dbo.ServiceOrders so
JOIN dbo.Customers c ON c.Id = so.CustomerId
JOIN dbo.Vehicles v ON v.Id = so.VehicleId
WHERE so.Status IN ('CHECKED_IN', 'DIAGNOSING', 'QUOTING', 'WAITING_APPROVAL', 'APPROVED', 'IN_PROGRESS')
ORDER BY so.Id DESC;

IF @ServiceOrderId IS NULL
BEGIN
    THROW 51000, 'Khong tim thay ServiceOrder phu hop de seed du lieu demo.', 1;
END;

IF @ActorStaffId IS NULL
BEGIN
    SELECT TOP 1 @ActorStaffId = s.Id
    FROM dbo.Staffs s
    WHERE s.IsDeleted = 0
    ORDER BY s.Id;
END;

IF @ActorStaffId IS NULL
BEGIN
    THROW 51001, 'Khong tim thay Staff de gan nguoi tao/duyet.', 1;
END;

IF @CustomerEmail IS NULL OR LTRIM(RTRIM(@CustomerEmail)) = ''
BEGIN
    SET @CustomerEmail = CONCAT('uat.customer.', @ServiceOrderId, '@mygarage.local');
END;

SET @Code = CONCAT('QT-UAT-INS-', FORMAT(@Now, 'yyyyMMddHHmmss'), '-', @ServiceOrderId);

BEGIN TRANSACTION;

SELECT TOP 1 @QuotationId = q.Id
FROM dbo.Quotations q
WHERE q.ServiceOrderId = @ServiceOrderId
  AND q.Code LIKE 'QT-UAT-INS-%'
ORDER BY q.Id DESC;

IF @QuotationId IS NULL
BEGIN
    INSERT INTO dbo.Quotations
    (
        Code,
        ServiceOrderId,
        Title,
        QuotationType,
        VersionNo,
        SubTotalAmount,
        TotalDiscount,
        TaxAmount,
        FinalAmount,
        TotalInsuranceAmount,
        TotalCustomerAmount,
        Status,
        ValidUntil,
        ServiceAdvisorId,
        InsuranceApprovalEvidenceUrl,
        TeamLeaderApprovalStatus,
        FinalApprovalStatus,
        CustomerDecisionStatus,
        IsAppliedToServiceOrder,
        IsReadyForSplitBilling,
        SplitBillingReadyAt,
        InternalNotes,
        CreatedAt,
        CreatedBy,
        UpdatedAt,
        UpdatedBy
    )
    VALUES
    (
        @Code,
        @ServiceOrderId,
        N'Bao gia V1 - Demo luong bao hiem E2E',
        'INSURANCE',
        1,
        @FinalAmount,
        0,
        0,
        @FinalAmount,
        @InsuranceAmount,
        @CustomerAmount,
        'PENDING_CUSTOMER',
        DATEADD(DAY, 15, @Now),
        @ActorStaffId,
        CONCAT('https://storage.example/mygarage/quotation/', @ServiceOrderId, '/evidence-demo.png'),
        'APPROVED',
        'APPROVED',
        'PENDING',
        0,
        0,
        NULL,
        N'SEED_UAT_INSURANCE_E2E',
        @Now,
        @ActorStaffId,
        @Now,
        @ActorStaffId
    );

    SET @QuotationId = SCOPE_IDENTITY();

    INSERT INTO dbo.QuotationDetails
    (
        QuotationId,
        ServiceOrderId,
        ItemType,
        ItemName,
        Quantity,
        UnitPriceAtTime,
        CostPriceAtTime,
        SubTotal,
        DiscountValue,
        TaxRate,
        TaxAmount,
        FinalAmount,
        PayerType,
        InsuranceCoveredAmount,
        CustomerCoveredAmount,
        InsuranceRequestedAmount,
        CustomerCoPayAmount,
        ApprovalStatus,
        IsBlocking,
        IsMandatory,
        IsReturnOldPart,
        CreatedAt,
        CreatedBy,
        UpdatedAt,
        UpdatedBy
    )
    VALUES
    (
        @QuotationId,
        @ServiceOrderId,
        'LABOR',
        N'Cong dai tu may (SL: 3)',
        3,
        13200000,
        NULL,
        39600000,
        0,
        0,
        0,
        39600000,
        'SPLIT',
        30000000,
        9600000,
        30000000,
        9600000,
        'APPROVED',
        1,
        1,
        0,
        @Now,
        @ActorStaffId,
        @Now,
        @ActorStaffId
    );

    INSERT INTO dbo.QuotationDetails
    (
        QuotationId,
        ServiceOrderId,
        ItemType,
        ItemName,
        Quantity,
        UnitPriceAtTime,
        CostPriceAtTime,
        SubTotal,
        DiscountValue,
        TaxRate,
        TaxAmount,
        FinalAmount,
        PayerType,
        InsuranceCoveredAmount,
        CustomerCoveredAmount,
        InsuranceRequestedAmount,
        CustomerCoPayAmount,
        ApprovalStatus,
        IsBlocking,
        IsMandatory,
        IsReturnOldPart,
        CreatedAt,
        CreatedBy,
        UpdatedAt,
        UpdatedBy
    )
    VALUES
    (
        @QuotationId,
        @ServiceOrderId,
        'PART',
        N'Phu tung bo sung khach tu thanh toan',
        1,
        500000,
        NULL,
        500000,
        0,
        0,
        0,
        500000,
        'CUSTOMER',
        0,
        500000,
        0,
        500000,
        'APPROVED',
        0,
        0,
        0,
        @Now,
        @ActorStaffId,
        @Now,
        @ActorStaffId
    );
END
ELSE
BEGIN
    UPDATE dbo.Quotations
    SET Status = 'PENDING_CUSTOMER',
        InsuranceApprovalEvidenceUrl = COALESCE(NULLIF(InsuranceApprovalEvidenceUrl, ''), CONCAT('https://storage.example/mygarage/quotation/', @ServiceOrderId, '/evidence-demo.png')),
        TeamLeaderApprovalStatus = 'APPROVED',
        FinalApprovalStatus = 'APPROVED',
        CustomerDecisionStatus = 'PENDING',
        IsAppliedToServiceOrder = 0,
        IsReadyForSplitBilling = 0,
        SplitBillingReadyAt = NULL,
        TotalInsuranceAmount = @InsuranceAmount,
        TotalCustomerAmount = @CustomerAmount,
        FinalAmount = @FinalAmount,
        UpdatedAt = @Now,
        UpdatedBy = @ActorStaffId
    WHERE Id = @QuotationId;

    DELETE FROM dbo.QuotationDetails WHERE QuotationId = @QuotationId;

    INSERT INTO dbo.QuotationDetails
    (
        QuotationId,
        ServiceOrderId,
        ItemType,
        ItemName,
        Quantity,
        UnitPriceAtTime,
        CostPriceAtTime,
        SubTotal,
        DiscountValue,
        TaxRate,
        TaxAmount,
        FinalAmount,
        PayerType,
        InsuranceCompanyId,
        InsuranceCoveredAmount,
        CustomerCoveredAmount,
        InsuranceRequestedAmount,
        CustomerCoPayAmount,
        ApprovalStatus,
        IsBlocking,
        IsMandatory,
        IsReturnOldPart,
        CreatedAt,
        CreatedBy,
        UpdatedAt,
        UpdatedBy
    )
    VALUES
    (
        @QuotationId,
        @ServiceOrderId,
        'LABOR',
        N'Cong dai tu may (SL: 3)',
        3,
        13200000,
        NULL,
        39600000,
        0,
        0,
        0,
        39600000,
        'SPLIT',
        1,
        30000000,
        9600000,
        30000000,
        9600000,
        'APPROVED',
        1,
        1,
        0,
        @Now,
        @ActorStaffId,
        @Now,
        @ActorStaffId
    );

    INSERT INTO dbo.QuotationDetails
    (
        QuotationId,
        ServiceOrderId,
        ItemType,
        ItemName,
        Quantity,
        UnitPriceAtTime,
        CostPriceAtTime,
        SubTotal,
        DiscountValue,
        TaxRate,
        TaxAmount,
        FinalAmount,
        PayerType,
        InsuranceCompanyId,
        InsuranceCoveredAmount,
        CustomerCoveredAmount,
        InsuranceRequestedAmount,
        CustomerCoPayAmount,
        ApprovalStatus,
        IsBlocking,
        IsMandatory,
        IsReturnOldPart,
        CreatedAt,
        CreatedBy,
        UpdatedAt,
        UpdatedBy
    )
    VALUES
    (
        @QuotationId,
        @ServiceOrderId,
        'PART',
        N'Phu tung bo sung khach tu thanh toan',
        1,
        500000,
        NULL,
        500000,
        0,
        0,
        0,
        500000,
        'CUSTOMER',
        1,
        0,
        500000,
        0,
        500000,
        'APPROVED',
        0,
        0,
        0,
        @Now,
        @ActorStaffId,
        @Now,
        @ActorStaffId
    );
END;

SET @RawToken = CONCAT('MG-UAT-', @QuotationId, '-', FORMAT(@Now, 'yyyyMMddHHmmss'));
SET @TokenHash = LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @RawToken), 2));

UPDATE dbo.QuotationAccessLinks
SET Status = 'REVOKED',
    UpdatedAt = @Now,
    UpdatedBy = @ActorStaffId
WHERE QuotationId = @QuotationId
  AND Status = 'ACTIVE';

INSERT INTO dbo.QuotationAccessLinks
(
    QuotationId,
    TokenHash,
    Status,
    ExpiresAt,
    SentToEmail,
    ClientNote,
    CreatedAt,
    CreatedBy,
    UpdatedAt,
    UpdatedBy,
    OtpAttemptCount
)
VALUES
(
    @QuotationId,
    @TokenHash,
    'ACTIVE',
    DATEADD(DAY, 15, @Now),
    @CustomerEmail,
    N'SEED_UAT_INSURANCE_E2E',
    @Now,
    @ActorStaffId,
    @Now,
    @ActorStaffId,
    0
);

UPDATE dbo.ServiceOrders
SET InsuranceQuoteState = 'PENDING_CUSTOMER',
    UpdatedAt = @Now,
    UpdatedBy = @ActorStaffId
WHERE Id = @ServiceOrderId;

COMMIT TRANSACTION;

SELECT
    q.Id AS QuotationId,
    q.Code AS QuotationCode,
    q.Status,
    q.TotalInsuranceAmount,
    q.TotalCustomerAmount,
    q.FinalAmount,
    so.Id AS ServiceOrderId,
    so.Code AS ServiceOrderCode,
    @Plate AS VehiclePlate,
    @CustomerEmail AS CustomerEmail,
    @RawToken AS RawTokenForUat,
    CONCAT(@PortalBase, '/', @RawToken) AS PublicLinkForUat
FROM dbo.Quotations q
JOIN dbo.ServiceOrders so ON so.Id = q.ServiceOrderId
WHERE q.Id = @QuotationId;
