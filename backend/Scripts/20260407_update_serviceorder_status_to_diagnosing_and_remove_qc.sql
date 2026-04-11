/*
  ServiceOrders status normalization:
  - INSPECTING -> DIAGNOSING
  - QC -> READY
  - Rebuild CK_ServiceOrders_Status without INSPECTING/QC

  Final allowed values:
  NEW, CHECKED_IN, WAITING, DIAGNOSING, QUOTING, WAITING_APPROVAL,
  APPROVED, IN_PROGRESS, REPAIRING, READY, WAITING_PAYMENT, CLOSED, CANCELED
*/

BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE dbo.ServiceOrders
    SET [Status] = 'DIAGNOSING'
    WHERE UPPER(LTRIM(RTRIM([Status]))) = 'INSPECTING';

    UPDATE dbo.ServiceOrders
    SET [Status] = 'READY'
    WHERE UPPER(LTRIM(RTRIM([Status]))) = 'QC';

    DECLARE @DropSql NVARCHAR(MAX) = N'';

    SELECT @DropSql = @DropSql
        + N'ALTER TABLE dbo.ServiceOrders DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';' + CHAR(10)
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.ServiceOrders')
      AND (
            cc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.ServiceOrders'), N'Status', 'ColumnId')
            OR cc.definition LIKE N'%[[]Status[]]%'
          );

    IF LEN(@DropSql) > 0
    BEGIN
        EXEC sp_executesql @DropSql;
    END;

    ALTER TABLE dbo.ServiceOrders
    WITH CHECK ADD CONSTRAINT CK_ServiceOrders_Status
    CHECK
    (
        [Status] IN
        (
            'NEW',
            'CHECKED_IN',
            'WAITING',
            'DIAGNOSING',
            'QUOTING',
            'WAITING_APPROVAL',
            'APPROVED',
            'IN_PROGRESS',
            'REPAIRING',
            'READY',
            'WAITING_PAYMENT',
            'CLOSED',
            'CANCELED'
        )
    );

    ALTER TABLE dbo.ServiceOrders
    CHECK CONSTRAINT CK_ServiceOrders_Status;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
