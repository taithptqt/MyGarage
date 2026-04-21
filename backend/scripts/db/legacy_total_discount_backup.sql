/* Backup legacy TotalDiscount values before dropping obsolete columns.
   Run this script once on the target database before DROP COLUMN. */

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.LegacyTotalDiscountBackup', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LegacyTotalDiscountBackup
    (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SnapshotAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
        EntityName NVARCHAR(64) NOT NULL,
        EntityId BIGINT NOT NULL,
        EntityCode NVARCHAR(64) NULL,
        TotalDiscount DECIMAL(18,2) NOT NULL
    );
END;

IF COL_LENGTH(N'dbo.Quotations', N'TotalDiscount') IS NOT NULL
BEGIN
    EXEC(N'
        INSERT INTO dbo.LegacyTotalDiscountBackup (EntityName, EntityId, EntityCode, TotalDiscount)
        SELECT
            N''Quotation'' AS EntityName,
            q.Id AS EntityId,
            q.Code AS EntityCode,
            ISNULL(q.TotalDiscount, 0) AS TotalDiscount
        FROM dbo.Quotations q;
    ');
END;

IF COL_LENGTH(N'dbo.ServiceOrders', N'TotalDiscount') IS NOT NULL
BEGIN
    EXEC(N'
        INSERT INTO dbo.LegacyTotalDiscountBackup (EntityName, EntityId, EntityCode, TotalDiscount)
        SELECT
            N''ServiceOrder'' AS EntityName,
            so.Id AS EntityId,
            so.Code AS EntityCode,
            ISNULL(so.TotalDiscount, 0) AS TotalDiscount
        FROM dbo.ServiceOrders so;
    ');
END;

SELECT
    EntityName,
    COUNT(1) AS BackupRowCount,
    SUM(TotalDiscount) AS TotalDiscountAmount
FROM dbo.LegacyTotalDiscountBackup
GROUP BY EntityName;
