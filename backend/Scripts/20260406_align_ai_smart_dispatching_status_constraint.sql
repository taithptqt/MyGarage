/*
  Align dbo.AI_SmartDispatching.Status CHECK constraint to support
  both legacy and new lifecycle values.

  Allowed values:
  - GENERATED
  - APPLIED
  - REJECTED
  - PENDING
*/

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @DropSql NVARCHAR(MAX) = N'';

    SELECT @DropSql = @DropSql
        + N'ALTER TABLE dbo.AI_SmartDispatching DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';' + CHAR(10)
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.AI_SmartDispatching')
      AND (
            cc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.AI_SmartDispatching'), N'Status', 'ColumnId')
            OR cc.definition LIKE N'%[[]Status[]]%'
          );

    IF LEN(@DropSql) > 0
    BEGIN
        EXEC sp_executesql @DropSql;
    END;

    ALTER TABLE dbo.AI_SmartDispatching
    WITH CHECK ADD CONSTRAINT CK_AI_SmartDispatching_Status
    CHECK
    (
        [Status] IS NULL OR
        [Status] IN
        (
            'GENERATED',
            'APPLIED',
            'REJECTED',
            'PENDING'
        )
    );

    ALTER TABLE dbo.AI_SmartDispatching
    CHECK CONSTRAINT CK_AI_SmartDispatching_Status;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
