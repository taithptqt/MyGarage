/*
  Rename legacy lead technician timestamp columns to generic technician timestamps.
  Safe to run multiple times.
*/

SET NOCOUNT ON;

IF COL_LENGTH('dbo.Diagnoses', 'LeadTechnicianAcceptedAt') IS NOT NULL
   AND COL_LENGTH('dbo.Diagnoses', 'TechnicianAcceptedAt') IS NULL
BEGIN
    EXEC sp_rename
        @objname = N'dbo.Diagnoses.LeadTechnicianAcceptedAt',
        @newname = N'TechnicianAcceptedAt',
        @objtype = N'COLUMN';
END;

IF COL_LENGTH('dbo.Diagnoses', 'LeadTechnicianCompletedAt') IS NOT NULL
   AND COL_LENGTH('dbo.Diagnoses', 'TechnicianCompletedAt') IS NULL
BEGIN
    EXEC sp_rename
        @objname = N'dbo.Diagnoses.LeadTechnicianCompletedAt',
        @newname = N'TechnicianCompletedAt',
        @objtype = N'COLUMN';
END;

IF COL_LENGTH('dbo.DiagnosisAssignments', 'LeadTechnicianAcceptedAt') IS NOT NULL
   AND COL_LENGTH('dbo.DiagnosisAssignments', 'TechnicianAcceptedAt') IS NULL
BEGIN
    EXEC sp_rename
        @objname = N'dbo.DiagnosisAssignments.LeadTechnicianAcceptedAt',
        @newname = N'TechnicianAcceptedAt',
        @objtype = N'COLUMN';
END;

IF COL_LENGTH('dbo.DiagnosisAssignments', 'LeadTechnicianCompletedAt') IS NOT NULL
   AND COL_LENGTH('dbo.DiagnosisAssignments', 'TechnicianCompletedAt') IS NULL
BEGIN
    EXEC sp_rename
        @objname = N'dbo.DiagnosisAssignments.LeadTechnicianCompletedAt',
        @newname = N'TechnicianCompletedAt',
        @objtype = N'COLUMN';
END;
