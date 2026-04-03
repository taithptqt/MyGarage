SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.Bays', 'ServiceArea') IS NULL
BEGIN
    ALTER TABLE [dbo].[Bays]
    ADD [ServiceArea] [varchar](50) NULL;
END
GO

DECLARE @dropServiceAreaChecksSql nvarchar(max) = N'';

SELECT @dropServiceAreaChecksSql =
    STRING_AGG(
        N'ALTER TABLE [dbo].[Bays] DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';',
        CHAR(10)
    )
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Bays')
  AND OBJECT_DEFINITION(cc.object_id) LIKE '%ServiceArea%';

IF @dropServiceAreaChecksSql IS NOT NULL AND LEN(@dropServiceAreaChecksSql) > 0
BEGIN
    EXEC sp_executesql @dropServiceAreaChecksSql;
END
GO

UPDATE [dbo].[Bays]
SET [ServiceArea] =
    CASE
        WHEN UPPER(LTRIM(RTRIM(ISNULL([ServiceArea], '')))) IN (
            'QUICK', 'DVN', 'FAST', 'MAINTENANCE', 'BAO DUONG', 'DICH VU NHANH'
        ) THEN 'QUICK'
        WHEN UPPER(LTRIM(RTRIM(ISNULL([ServiceArea], '')))) IN (
            'REPAIR', 'SC', 'SUA CHUA'
        ) THEN 'REPAIR'
        WHEN UPPER(LTRIM(RTRIM(ISNULL([BayType], '')))) LIKE '%QUICK%'
          OR UPPER(LTRIM(RTRIM(ISNULL([BayType], '')))) LIKE '%DVN%'
          OR UPPER(LTRIM(RTRIM(ISNULL([BayType], '')))) LIKE '%FAST%'
          OR UPPER(LTRIM(RTRIM(ISNULL([BayType], '')))) LIKE '%MAINTENANCE%'
          OR UPPER(LTRIM(RTRIM(ISNULL([BayType], '')))) LIKE '%BAO DUONG%'
          OR UPPER(LTRIM(RTRIM(ISNULL([BayType], '')))) LIKE '%DICH VU NHANH%'
          OR UPPER(LTRIM(RTRIM(ISNULL([BayType], '')))) LIKE '%NHANH%' THEN 'QUICK'
        ELSE 'REPAIR'
    END;
GO

ALTER TABLE [dbo].[Bays] ALTER COLUMN [ServiceArea] [varchar](50) NOT NULL;
GO

ALTER TABLE [dbo].[Bays] WITH CHECK
ADD CONSTRAINT [CK_Bays_ServiceArea]
CHECK ([ServiceArea] IN ('QUICK', 'REPAIR'));
GO
