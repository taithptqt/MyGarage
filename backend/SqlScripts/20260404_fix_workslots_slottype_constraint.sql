-- Normalize WorkSlots.SlotType check to remove accidental trailing spaces
-- and enforce values in a trim-safe way.
IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_WorkSlots_SlotType'
      AND parent_object_id = OBJECT_ID(N'dbo.WorkSlots')
)
BEGIN
    ALTER TABLE [dbo].[WorkSlots] DROP CONSTRAINT [CK_WorkSlots_SlotType];
END
GO

ALTER TABLE [dbo].[WorkSlots] WITH CHECK
ADD CONSTRAINT [CK_WorkSlots_SlotType]
CHECK (UPPER(LTRIM(RTRIM([SlotType]))) IN (
    'WALK_IN',
    'APPOINTMENT',
    'OVERNIGHT_LOCK'
));
GO

