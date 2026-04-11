/*
  Align dbo.ServiceOrders.OrderCategory to the 6 service-category codes:
  - MAINTENANCE
  - GENERAL_REPAIR
  - QUICK_SERVICE
  - INSURANCE_REPAIR
  - REWORK
  - WARRANTLY

  This script:
  1) Normalizes known legacy values.
  2) Drops existing OrderCategory CHECK constraints.
  3) Recreates a strict CHECK constraint with exactly the 6 codes above.
*/

BEGIN TRY
    BEGIN TRANSACTION;

    -- Normalize known legacy aliases before enforcing strict constraint.
    UPDATE dbo.ServiceOrders
    SET OrderCategory = 'WARRANTLY'
    WHERE UPPER(LTRIM(RTRIM(ISNULL(OrderCategory, '')))) = 'WARRANTY';

    UPDATE dbo.ServiceOrders
    SET OrderCategory = 'GENERAL_REPAIR'
    WHERE UPPER(LTRIM(RTRIM(ISNULL(OrderCategory, '')))) = 'DIAGNOSIS';

    -- Guardrail: fail if any unexpected values remain.
    IF EXISTS
    (
        SELECT 1
        FROM dbo.ServiceOrders
        WHERE OrderCategory IS NOT NULL
          AND UPPER(LTRIM(RTRIM(OrderCategory))) NOT IN
          (
              'MAINTENANCE',
              'GENERAL_REPAIR',
              'QUICK_SERVICE',
              'INSURANCE_REPAIR',
              'REWORK',
              'WARRANTLY'
          )
    )
    BEGIN
        THROW 50001, 'ServiceOrders.OrderCategory contains unsupported values outside the 6 service-category codes.', 1;
    END;

    DECLARE @DropSql NVARCHAR(MAX) = N'';

    SELECT @DropSql = @DropSql
        + N'ALTER TABLE dbo.ServiceOrders DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';' + CHAR(10)
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.ServiceOrders')
      AND (
            cc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.ServiceOrders'), N'OrderCategory', 'ColumnId')
            OR cc.definition LIKE N'%[[]OrderCategory[]]%'
          );

    IF LEN(@DropSql) > 0
    BEGIN
        EXEC sp_executesql @DropSql;
    END;

    ALTER TABLE dbo.ServiceOrders
    WITH CHECK ADD CONSTRAINT CK_ServiceOrders_OrderCategory
    CHECK
    (
        [OrderCategory] IS NULL OR
        [OrderCategory] IN
        (
            'MAINTENANCE',
            'GENERAL_REPAIR',
            'QUICK_SERVICE',
            'INSURANCE_REPAIR',
            'REWORK',
            'WARRANTLY'
        )
    );

    ALTER TABLE dbo.ServiceOrders
    CHECK CONSTRAINT CK_ServiceOrders_OrderCategory;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
