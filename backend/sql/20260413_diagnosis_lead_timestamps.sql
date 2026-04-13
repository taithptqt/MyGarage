/*
  Diagnosis timeline columns update (2026-04-13)
  1) Remove StartedAt
  2) Add TechnicianAcceptedAt
  3) Add TechnicianCompletedAt
*/

SET NOCOUNT ON;

IF COL_LENGTH('Diagnoses', 'TechnicianAcceptedAt') IS NULL
BEGIN
    ALTER TABLE Diagnoses ADD TechnicianAcceptedAt datetime2 NULL;
END;

IF COL_LENGTH('Diagnoses', 'TechnicianCompletedAt') IS NULL
BEGIN
    ALTER TABLE Diagnoses ADD TechnicianCompletedAt datetime2 NULL;
END;

IF COL_LENGTH('Diagnoses', 'StartedAt') IS NOT NULL
BEGIN
    UPDATE d
    SET TechnicianAcceptedAt = COALESCE(d.TechnicianAcceptedAt, d.StartedAt)
    FROM Diagnoses d
    WHERE d.StartedAt IS NOT NULL;

    ALTER TABLE Diagnoses DROP COLUMN StartedAt;
END;

IF COL_LENGTH('Diagnoses', 'DiagnosedAt') IS NOT NULL
BEGIN
    UPDATE d
    SET TechnicianCompletedAt = COALESCE(d.TechnicianCompletedAt, d.DiagnosedAt)
    FROM Diagnoses d
    WHERE d.DiagnosedAt IS NOT NULL;
END;


