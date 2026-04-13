/*
  Diagnosis schema update (2026-04-13):
  1) Drop AssignedByTeamLeaderId
  2) Replace FinalApprovedAt -> TeamLeaderNote (nvarchar(500))
  3) Rename FinalApprovalNote -> ForemanNote
*/

SET NOCOUNT ON;

IF COL_LENGTH('Diagnoses', 'TeamLeaderNote') IS NULL
BEGIN
    ALTER TABLE Diagnoses ADD TeamLeaderNote nvarchar(500) NULL;
END;

IF COL_LENGTH('Diagnoses', 'FinalApprovedAt') IS NOT NULL
BEGIN
    UPDATE d
    SET TeamLeaderNote = COALESCE(
        NULLIF(d.TeamLeaderNote, ''),
        CONVERT(nvarchar(30), d.FinalApprovedAt, 126)
    )
    FROM Diagnoses d
    WHERE d.FinalApprovedAt IS NOT NULL;

    ALTER TABLE Diagnoses DROP COLUMN FinalApprovedAt;
END;

IF COL_LENGTH('Diagnoses', 'FinalApprovalNote') IS NOT NULL
    AND COL_LENGTH('Diagnoses', 'ForemanNote') IS NULL
BEGIN
    EXEC sp_rename 'Diagnoses.FinalApprovalNote', 'ForemanNote', 'COLUMN';
END;

IF COL_LENGTH('Diagnoses', 'ForemanNote') IS NULL
BEGIN
    ALTER TABLE Diagnoses ADD ForemanNote nvarchar(500) NULL;
END;

IF COL_LENGTH('Diagnoses', 'AssignedByTeamLeaderId') IS NOT NULL
BEGIN
    ALTER TABLE Diagnoses DROP COLUMN AssignedByTeamLeaderId;
END;

