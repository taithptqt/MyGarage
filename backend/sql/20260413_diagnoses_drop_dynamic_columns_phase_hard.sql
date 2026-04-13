SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Diagnoses', N'U') IS NULL
BEGIN
    PRINT N'[SKIP] Table dbo.Diagnoses does not exist.';
    RETURN;
END;

DECLARE @DropCols TABLE (ColName sysname PRIMARY KEY);
INSERT INTO @DropCols (ColName)
VALUES
(N'TechnicianId'),
(N'AssistantTechnicianId'),
(N'AssignedBayId'),
(N'AssignedTeamId'),
(N'AssignedAt'),
(N'TechnicianAcceptedAt'),
(N'TechnicianCompletedAt'),
(N'SupportRequestedAt'),
(N'SupportRequestedBy'),
(N'SupportRequestNote'),
(N'CancellationStatus'),
(N'CancellationReason'),
(N'CancellationRequestedAt'),
(N'CancellationRequestedBy'),
(N'CancellationDecisionAt'),
(N'CancellationDecisionBy'),
(N'CancellationDecisionNote'),
(N'ApprovedByForemanId'),
(N'ApprovedByForemanAt'),
(N'ApprovedByTeamLeaderAt'),
(N'TeamLeaderNote'),
(N'ForemanNote'),
(N'DispatchCollaborationMeta');

DECLARE @Sql nvarchar(max);

;WITH fk_targets AS
(
    SELECT DISTINCT fk.name AS ConstraintName
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    INNER JOIN sys.tables t ON t.object_id = fk.parent_object_id
    INNER JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = fkc.parent_column_id
    WHERE t.name = N'Diagnoses'
      AND SCHEMA_NAME(t.schema_id) = N'dbo'
      AND c.name IN (SELECT ColName FROM @DropCols)
)
SELECT @Sql = STRING_AGG(
    N'ALTER TABLE dbo.Diagnoses DROP CONSTRAINT ' + QUOTENAME(ConstraintName) + N';',
    CHAR(10))
FROM fk_targets;

IF @Sql IS NOT NULL AND LEN(@Sql) > 0
BEGIN
    PRINT N'[INFO] Dropping FK constraints on dynamic columns...';
    EXEC sp_executesql @Sql;
END;

SET @Sql = NULL;

;WITH index_targets AS
(
    SELECT DISTINCT i.name AS IndexName
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    INNER JOIN sys.tables t ON t.object_id = i.object_id
    WHERE t.name = N'Diagnoses'
      AND SCHEMA_NAME(t.schema_id) = N'dbo'
      AND c.name IN (SELECT ColName FROM @DropCols)
      AND i.is_primary_key = 0
      AND i.is_unique_constraint = 0
      AND i.name IS NOT NULL
)
SELECT @Sql = STRING_AGG(
    N'DROP INDEX ' + QUOTENAME(IndexName) + N' ON dbo.Diagnoses;',
    CHAR(10))
FROM index_targets;

IF @Sql IS NOT NULL AND LEN(@Sql) > 0
BEGIN
    PRINT N'[INFO] Dropping indexes on dynamic columns...';
    EXEC sp_executesql @Sql;
END;

SET @Sql = NULL;

;WITH default_targets AS
(
    SELECT DISTINCT dc.name AS ConstraintName
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = N'Diagnoses'
      AND SCHEMA_NAME(t.schema_id) = N'dbo'
      AND c.name IN (SELECT ColName FROM @DropCols)
)
SELECT @Sql = STRING_AGG(
    N'ALTER TABLE dbo.Diagnoses DROP CONSTRAINT ' + QUOTENAME(ConstraintName) + N';',
    CHAR(10))
FROM default_targets;

IF @Sql IS NOT NULL AND LEN(@Sql) > 0
BEGIN
    PRINT N'[INFO] Dropping default constraints on dynamic columns...';
    EXEC sp_executesql @Sql;
END;

DECLARE @ColName sysname;
DECLARE col_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT ColName
    FROM @DropCols
    ORDER BY ColName;

OPEN col_cursor;
FETCH NEXT FROM col_cursor INTO @ColName;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        INNER JOIN sys.tables t ON t.object_id = c.object_id
        WHERE t.name = N'Diagnoses'
          AND SCHEMA_NAME(t.schema_id) = N'dbo'
          AND c.name = @ColName)
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.Diagnoses DROP COLUMN ' + QUOTENAME(@ColName) + N';';
        EXEC sp_executesql @Sql;
        PRINT N'[OK] Dropped column dbo.Diagnoses.' + @ColName;
    END
    ELSE
    BEGIN
        PRINT N'[SKIP] Column dbo.Diagnoses.' + @ColName + N' not found.';
    END

    FETCH NEXT FROM col_cursor INTO @ColName;
END;

CLOSE col_cursor;
DEALLOCATE col_cursor;

PRINT N'[DONE] Hard phase dynamic-column drop completed for dbo.Diagnoses.';

