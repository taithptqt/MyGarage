-- Diagnosis schema update for team-leader assignment/approval tracking
-- 1) Replace FinalApprovedByManagerId with ApprovedByTeamLeaderAt (datetime2)
-- 2) Add ApprovedByForemanAt (datetime2)
-- 3) Add AssignedByTeamLeaderId (bigint)

IF COL_LENGTH('Diagnoses', 'FinalApprovedByManagerId') IS NOT NULL
BEGIN
    DECLARE @dropFkSql nvarchar(max) = N'';
    DECLARE @dropIxSql nvarchar(max) = N'';

    SELECT @dropFkSql = @dropFkSql + N'ALTER TABLE [Diagnoses] DROP CONSTRAINT [' + fk.name + N'];'
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
    WHERE fk.parent_object_id = OBJECT_ID(N'Diagnoses')
      AND c.name = N'FinalApprovedByManagerId';

    IF LEN(@dropFkSql) > 0 EXEC sp_executesql @dropFkSql;

    SELECT @dropIxSql = @dropIxSql + N'DROP INDEX [' + i.name + N'] ON [Diagnoses];'
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = OBJECT_ID(N'Diagnoses')
      AND i.is_primary_key = 0
      AND i.is_unique_constraint = 0
      AND c.name = N'FinalApprovedByManagerId';

    IF LEN(@dropIxSql) > 0 EXEC sp_executesql @dropIxSql;

    ALTER TABLE Diagnoses DROP COLUMN FinalApprovedByManagerId;
END;

IF COL_LENGTH('Diagnoses', 'ApprovedByTeamLeaderAt') IS NULL
BEGIN
    ALTER TABLE Diagnoses ADD ApprovedByTeamLeaderAt datetime2 NULL;
END;

IF COL_LENGTH('Diagnoses', 'ApprovedByForemanAt') IS NULL
BEGIN
    ALTER TABLE Diagnoses ADD ApprovedByForemanAt datetime2 NULL;
END;

IF COL_LENGTH('Diagnoses', 'AssignedByTeamLeaderId') IS NULL
BEGIN
    ALTER TABLE Diagnoses ADD AssignedByTeamLeaderId bigint NULL;
END;
