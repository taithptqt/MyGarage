/*
  Normalize status spelling from CANCELLED -> CANCELED
  and align check constraints for status columns.
  Date: 2026-04-13
*/
SET NOCOUNT ON;

PRINT '=== AUDIT BEFORE ===';
SELECT 'Appointments.Status' AS Target, UPPER(Status) AS StatusValue, COUNT(*) AS Cnt
FROM Appointments
WHERE Status IS NOT NULL AND UPPER(Status) IN ('CANCELED', 'CANCELLED')
GROUP BY UPPER(Status)
UNION ALL
SELECT 'WorkSlots.Status', UPPER(Status), COUNT(*)
FROM WorkSlots
WHERE Status IS NOT NULL AND UPPER(Status) IN ('CANCELED', 'CANCELLED')
GROUP BY UPPER(Status)
UNION ALL
SELECT 'PartInventoryReservations.Status', UPPER(Status), COUNT(*)
FROM PartInventoryReservations
WHERE Status IS NOT NULL AND UPPER(Status) IN ('CANCELED', 'CANCELLED')
GROUP BY UPPER(Status)
UNION ALL
SELECT 'MaterialRequests.Status', UPPER(Status), COUNT(*)
FROM MaterialRequests
WHERE Status IS NOT NULL AND UPPER(Status) IN ('CANCELED', 'CANCELLED')
GROUP BY UPPER(Status)
UNION ALL
SELECT 'MaterialRequestDetails.Status', UPPER(Status), COUNT(*)
FROM MaterialRequestDetails
WHERE Status IS NOT NULL AND UPPER(Status) IN ('CANCELED', 'CANCELLED')
GROUP BY UPPER(Status);

BEGIN TRANSACTION;

/* WorkSlots constraint */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_WorkSlots_Status')
BEGIN
    ALTER TABLE dbo.WorkSlots DROP CONSTRAINT CK_WorkSlots_Status;
END
ALTER TABLE dbo.WorkSlots WITH CHECK ADD CONSTRAINT CK_WorkSlots_Status
CHECK (
    [Status] IN (
        'LOCKED',
        'RESERVED',
        'ARRIVED',
        'IN_PROGRESS',
        'DIAGNOSED_WAITING',
        'COMPLETED',
        'NOSHOW',
        'CANCELED',
        'OVERNIGHT_HOLD'
    )
);

/* PartInventoryReservations constraint */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PartInventoryReservations_Status')
BEGIN
    ALTER TABLE dbo.PartInventoryReservations DROP CONSTRAINT CK_PartInventoryReservations_Status;
END
ALTER TABLE dbo.PartInventoryReservations WITH CHECK ADD CONSTRAINT CK_PartInventoryReservations_Status
CHECK ([Status] IN ('RESERVED', 'FULFILLED', 'CANCELED'));

/* Appointments constraint */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Appointments_Status')
BEGIN
    ALTER TABLE dbo.Appointments DROP CONSTRAINT CK_Appointments_Status;
END
ALTER TABLE dbo.Appointments WITH CHECK ADD CONSTRAINT CK_Appointments_Status
CHECK (
    UPPER(LTRIM(RTRIM([Status]))) IN (
        'NO_SHOW',
        'CANCELED',
        'ARRIVED_LATE',
        'ARRIVED',
        'CONFIRMED',
        'LATE',
        'PENDING'
    )
);

/* MaterialRequests status constraints (auto-generated names possible) */
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql + N'ALTER TABLE dbo.MaterialRequests DROP CONSTRAINT [' + cc.name + N'];'
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('dbo.MaterialRequests')
  AND cc.definition LIKE '%[Status]%';
IF @sql <> N'' EXEC sp_executesql @sql;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.MaterialRequests') AND name = 'CK_MaterialRequests_Status')
BEGIN
    ALTER TABLE dbo.MaterialRequests WITH CHECK ADD CONSTRAINT CK_MaterialRequests_Status
    CHECK ([Status] IN ('PENDING', 'PARTIAL', 'COMPLETED', 'CANCELED'));
END

/* MaterialRequestDetails status constraints (auto-generated names possible) */
SET @sql = N'';
SELECT @sql = @sql + N'ALTER TABLE dbo.MaterialRequestDetails DROP CONSTRAINT [' + cc.name + N'];'
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('dbo.MaterialRequestDetails')
  AND cc.definition LIKE '%[Status]%';
IF @sql <> N'' EXEC sp_executesql @sql;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.MaterialRequestDetails') AND name = 'CK_MaterialRequestDetails_Status')
BEGIN
    ALTER TABLE dbo.MaterialRequestDetails WITH CHECK ADD CONSTRAINT CK_MaterialRequestDetails_Status
    CHECK ([Status] IN ('PENDING', 'PARTIAL', 'COMPLETED', 'CANCELED'));
END

/* Data normalization */
UPDATE Appointments
SET Status = 'CANCELED'
WHERE Status IS NOT NULL AND UPPER(Status) = 'CANCELLED';

UPDATE WorkSlots
SET Status = 'CANCELED'
WHERE Status IS NOT NULL AND UPPER(Status) = 'CANCELLED';

UPDATE PartInventoryReservations
SET Status = 'CANCELED'
WHERE Status IS NOT NULL AND UPPER(Status) = 'CANCELLED';

UPDATE MaterialRequests
SET Status = 'CANCELED'
WHERE Status IS NOT NULL AND UPPER(Status) = 'CANCELLED';

UPDATE MaterialRequestDetails
SET Status = 'CANCELED'
WHERE Status IS NOT NULL AND UPPER(Status) = 'CANCELLED';

COMMIT TRANSACTION;

PRINT '=== AUDIT AFTER ===';
SELECT 'Appointments.Status' AS Target, UPPER(Status) AS StatusValue, COUNT(*) AS Cnt
FROM Appointments
WHERE Status IS NOT NULL AND UPPER(Status) IN ('CANCELED', 'CANCELLED')
GROUP BY UPPER(Status)
UNION ALL
SELECT 'WorkSlots.Status', UPPER(Status), COUNT(*)
FROM WorkSlots
WHERE Status IS NOT NULL AND UPPER(Status) IN ('CANCELED', 'CANCELLED')
GROUP BY UPPER(Status)
UNION ALL
SELECT 'PartInventoryReservations.Status', UPPER(Status), COUNT(*)
FROM PartInventoryReservations
WHERE Status IS NOT NULL AND UPPER(Status) IN ('CANCELED', 'CANCELLED')
GROUP BY UPPER(Status)
UNION ALL
SELECT 'MaterialRequests.Status', UPPER(Status), COUNT(*)
FROM MaterialRequests
WHERE Status IS NOT NULL AND UPPER(Status) IN ('CANCELED', 'CANCELLED')
GROUP BY UPPER(Status)
UNION ALL
SELECT 'MaterialRequestDetails.Status', UPPER(Status), COUNT(*)
FROM MaterialRequestDetails
WHERE Status IS NOT NULL AND UPPER(Status) IN ('CANCELED', 'CANCELLED')
GROUP BY UPPER(Status);
