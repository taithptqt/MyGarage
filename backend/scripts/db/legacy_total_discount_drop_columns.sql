/* Drop obsolete TotalDiscount columns after backup.
   Prerequisite: run legacy_total_discount_backup.sql successfully. */

SET NOCOUNT ON;

DECLARE @sql NVARCHAR(MAX);

IF COL_LENGTH(N'dbo.Quotations', N'TotalDiscount') IS NOT NULL
BEGIN
    SET @sql = N'';
    SELECT @sql = @sql + N'ALTER TABLE dbo.Quotations DROP CONSTRAINT [' + dc.name + N'];'
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Quotations')
      AND c.name = N'TotalDiscount';

    IF LEN(@sql) > 0
        EXEC sp_executesql @sql;

    ALTER TABLE dbo.Quotations DROP COLUMN TotalDiscount;
END;

IF COL_LENGTH(N'dbo.ServiceOrders', N'TotalDiscount') IS NOT NULL
BEGIN
    SET @sql = N'';
    SELECT @sql = @sql + N'ALTER TABLE dbo.ServiceOrders DROP CONSTRAINT [' + dc.name + N'];'
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.ServiceOrders')
      AND c.name = N'TotalDiscount';

    IF LEN(@sql) > 0
        EXEC sp_executesql @sql;

    ALTER TABLE dbo.ServiceOrders DROP COLUMN TotalDiscount;
END;

SELECT
    'Column_Quotations_TotalDiscount_Exists' AS CheckName,
    COUNT(1) AS CheckValue
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Quotations'
  AND COLUMN_NAME = 'TotalDiscount'
UNION ALL
SELECT
    'Column_ServiceOrders_TotalDiscount_Exists' AS CheckName,
    COUNT(1) AS CheckValue
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ServiceOrders'
  AND COLUMN_NAME = 'TotalDiscount';
