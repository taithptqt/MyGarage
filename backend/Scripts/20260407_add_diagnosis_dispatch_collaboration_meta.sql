SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.Diagnoses', N'DispatchCollaborationMeta') IS NULL
BEGIN
    ALTER TABLE dbo.Diagnoses
        ADD DispatchCollaborationMeta NVARCHAR(4000) NULL;
END;
