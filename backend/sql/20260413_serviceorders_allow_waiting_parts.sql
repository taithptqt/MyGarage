/*
  Ensure ServiceOrders.Status check constraint allows WAITING_PARTS.
  Date: 2026-04-13
*/
SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ServiceOrders_Status')
BEGIN
    ALTER TABLE dbo.ServiceOrders DROP CONSTRAINT CK_ServiceOrders_Status;
END

ALTER TABLE dbo.ServiceOrders WITH CHECK ADD CONSTRAINT CK_ServiceOrders_Status
CHECK (
    [Status] IN (
        'NEW',
        'CHECKED_IN',
        'WAITING',
        'DIAGNOSING',
        'QUOTING',
        'WAITING_APPROVAL',
        'APPROVED',
        'WAITING_PARTS',
        'IN_PROGRESS',
        'REPAIRING',
        'READY',
        'WAITING_PAYMENT',
        'CLOSED',
        'CANCELED'
    )
);
