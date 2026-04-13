/*
  Rename DiagnoseLogs -> DiagnosisAssignments (2026-04-13)
  Keep existing data and normalize key/index names.
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.DiagnosisAssignments', N'U') IS NULL
   AND OBJECT_ID(N'dbo.DiagnoseLogs', N'U') IS NOT NULL
BEGIN
    EXEC sp_rename 'dbo.DiagnoseLogs', 'DiagnosisAssignments';
END;

IF OBJECT_ID(N'dbo.DiagnosisAssignments', N'U') IS NULL
BEGIN
    RETURN;
END;

IF EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE [type] = 'PK'
      AND [name] = 'PK_DiagnoseLogs'
      AND [parent_object_id] = OBJECT_ID('dbo.DiagnosisAssignments')
)
BEGIN
    EXEC sp_rename 'PK_DiagnoseLogs', 'PK_DiagnosisAssignments', 'OBJECT';
END;

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_DiagnoseLogs_Diagnoses'
      AND [parent_object_id] = OBJECT_ID('dbo.DiagnosisAssignments')
)
BEGIN
    EXEC sp_rename 'FK_DiagnoseLogs_Diagnoses', 'FK_DiagnosisAssignments_Diagnoses', 'OBJECT';
END;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_DiagnoseLogs_DiagnoseId_CreatedAt'
      AND [object_id] = OBJECT_ID('dbo.DiagnosisAssignments')
)
BEGIN
    EXEC sp_rename 'dbo.DiagnosisAssignments.IX_DiagnoseLogs_DiagnoseId_CreatedAt', 'IX_DiagnosisAssignments_DiagnosisId_CreatedAt', 'INDEX';
END;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_DiagnoseLogs_DiagnosisId_CreatedAt'
      AND [object_id] = OBJECT_ID('dbo.DiagnosisAssignments')
)
BEGIN
    EXEC sp_rename 'dbo.DiagnosisAssignments.IX_DiagnoseLogs_DiagnosisId_CreatedAt', 'IX_DiagnosisAssignments_DiagnosisId_CreatedAt', 'INDEX';
END;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_DiagnoseLogs_Status_CreatedAt'
      AND [object_id] = OBJECT_ID('dbo.DiagnosisAssignments')
)
BEGIN
    EXEC sp_rename 'dbo.DiagnosisAssignments.IX_DiagnoseLogs_Status_CreatedAt', 'IX_DiagnosisAssignments_Status_CreatedAt', 'INDEX';
END;
