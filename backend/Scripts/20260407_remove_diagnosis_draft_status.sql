SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Diagnoses', N'U') IS NULL
    BEGIN
        THROW 50001, N'Bang dbo.Diagnoses khong ton tai.', 1;
    END;

    UPDATE dbo.Diagnoses
    SET Status = N'PENDING'
    WHERE UPPER(LTRIM(RTRIM(ISNULL(Status, N'')))) = N'DRAFT';

    DECLARE @dropStatusCheckSql NVARCHAR(MAX) = N'';

    SELECT @dropStatusCheckSql = @dropStatusCheckSql +
        N'ALTER TABLE dbo.Diagnoses DROP CONSTRAINT [' + cc.name + N'];' + CHAR(10)
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Diagnoses')
      AND cc.definition LIKE N'%Status%'
      AND cc.definition LIKE N'%DRAFT%';

    IF LEN(@dropStatusCheckSql) > 0
    BEGIN
        EXEC sys.sp_executesql @dropStatusCheckSql;
    END;

    DECLARE @statusDefaultConstraint SYSNAME;

    SELECT TOP (1) @statusDefaultConstraint = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Diagnoses')
      AND c.name = N'Status';

    IF @statusDefaultConstraint IS NOT NULL
    BEGIN
        EXEC (N'ALTER TABLE dbo.Diagnoses DROP CONSTRAINT [' + @statusDefaultConstraint + N'];');
    END;

    ALTER TABLE dbo.Diagnoses
        ADD CONSTRAINT DF_Diagnoses_Status DEFAULT (N'PENDING') FOR Status;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints cc
        WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Diagnoses')
          AND cc.definition LIKE N'%Status%'
    )
    BEGIN
        ALTER TABLE dbo.Diagnoses
            ADD CONSTRAINT CK_Diagnoses_Status_Allowed
            CHECK (UPPER(LTRIM(RTRIM(Status))) IN (N'PENDING', N'ASSIGNED', N'COMPLETED'));
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
