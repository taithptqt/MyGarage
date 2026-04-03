SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @dropAppointmentStatusChecksSql nvarchar(max) = N'';

SELECT @dropAppointmentStatusChecksSql =
    STRING_AGG(
        N'ALTER TABLE [dbo].[Appointments] DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';',
        CHAR(10)
    )
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Appointments')
  AND OBJECT_DEFINITION(cc.object_id) LIKE '%[Status]%';

IF @dropAppointmentStatusChecksSql IS NOT NULL AND LEN(@dropAppointmentStatusChecksSql) > 0
BEGIN
    EXEC sp_executesql @dropAppointmentStatusChecksSql;
END
GO

ALTER TABLE [dbo].[Appointments] WITH CHECK
ADD CONSTRAINT [CK_Appointments_Status]
CHECK (UPPER(LTRIM(RTRIM([Status]))) IN (
    'PENDING',
    'LATE',
    'CONFIRMED',
    'ARRIVED',
    'ARRIVED_LATE',
    'CANCELED',
    'CANCELLED',
    'NO_SHOW',
    'COMPLETED'
));
GO
