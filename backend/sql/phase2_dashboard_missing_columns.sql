/*
Phase 2 - Dashboard V2 compatibility patch
Run manually on SQL Server database before/after deploying backend update.
This script is idempotent: it only adds missing columns.
*/

SET NOCOUNT ON;

/* =========================
   Invoices
   ========================= */
IF COL_LENGTH('dbo.Invoices', 'InvoiceCode') IS NULL
BEGIN
    ALTER TABLE dbo.Invoices
    ADD InvoiceCode nvarchar(64) NULL;
END;

IF COL_LENGTH('dbo.Invoices', 'InvoiceDate') IS NULL
BEGIN
    ALTER TABLE dbo.Invoices
    ADD InvoiceDate datetime2 NULL;
END;

IF COL_LENGTH('dbo.Invoices', 'DueDate') IS NULL
BEGIN
    ALTER TABLE dbo.Invoices
    ADD DueDate datetime2 NULL;
END;

IF COL_LENGTH('dbo.Invoices', 'OutstandingAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Invoices
    ADD OutstandingAmount decimal(18,2) NULL;
END;

/* Backfill safe defaults (dynamic SQL avoids compile-time missing-column errors) */
IF COL_LENGTH('dbo.Invoices', 'InvoiceCode') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE dbo.Invoices
        SET InvoiceCode = COALESCE(InvoiceCode, CONCAT(''INV-'', Id))
        WHERE InvoiceCode IS NULL;';
END;

IF COL_LENGTH('dbo.Invoices', 'InvoiceDate') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE dbo.Invoices
        SET InvoiceDate = COALESCE(InvoiceDate, IssuedAt, CreatedAt)
        WHERE InvoiceDate IS NULL;';
END;

IF COL_LENGTH('dbo.Invoices', 'OutstandingAmount') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE dbo.Invoices
        SET OutstandingAmount = COALESCE(OutstandingAmount, ISNULL(TotalAmount,0) - ISNULL(PaidAmount,0))
        WHERE OutstandingAmount IS NULL;';
END;

/* =========================
   Payments
   ========================= */
IF COL_LENGTH('dbo.Payments', 'PaymentMethodId') IS NULL
BEGIN
    ALTER TABLE dbo.Payments
    ADD PaymentMethodId bigint NULL;
END;

IF COL_LENGTH('dbo.Payments', 'ReferenceCode') IS NULL
BEGIN
    ALTER TABLE dbo.Payments
    ADD ReferenceCode nvarchar(100) NULL;
END;

/* Sync PaymentMethodId from MethodId if available */
IF COL_LENGTH('dbo.Payments', 'MethodId') IS NOT NULL
   AND COL_LENGTH('dbo.Payments', 'PaymentMethodId') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE dbo.Payments
        SET PaymentMethodId = COALESCE(PaymentMethodId, MethodId)
        WHERE PaymentMethodId IS NULL;';
END;

PRINT 'phase2_dashboard_missing_columns.sql completed.';
