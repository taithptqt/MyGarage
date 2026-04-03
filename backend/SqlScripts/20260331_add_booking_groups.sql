SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.BookingGroups', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BookingGroups](
        [Id] [bigint] IDENTITY(1,1) NOT NULL,
        [GroupCode] [varchar](50) NOT NULL,
        [CustomerId] [bigint] NOT NULL,
        [BranchId] [bigint] NOT NULL,
        [Status] [varchar](30) NOT NULL CONSTRAINT [DF_BookingGroups_Status] DEFAULT ('PENDING'),
        [TotalAppointments] [int] NOT NULL CONSTRAINT [DF_BookingGroups_TotalAppointments] DEFAULT ((0)),
        [ConfirmedAppointments] [int] NOT NULL CONSTRAINT [DF_BookingGroups_ConfirmedAppointments] DEFAULT ((0)),
        [CanceledAppointments] [int] NOT NULL CONSTRAINT [DF_BookingGroups_CanceledAppointments] DEFAULT ((0)),
        [ConfirmedAt] [datetime] NULL,
        [CanceledAt] [datetime] NULL,
        [CancelReason] [nvarchar](500) NULL,
        [CustomerNote] [nvarchar](max) NULL,
        [CreatedAt] [datetime] NULL CONSTRAINT [DF_BookingGroups_CreatedAt] DEFAULT (getdate()),
        [CreatedBy] [bigint] NULL,
        [UpdatedAt] [datetime] NULL CONSTRAINT [DF_BookingGroups_UpdatedAt] DEFAULT (getdate()),
        [UpdatedBy] [bigint] NULL,
        CONSTRAINT [PK_BookingGroups] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [CK_BookingGroups_Status] CHECK ([Status] IN ('PENDING','CONFIRMED','PARTIAL_CONFIRMED','CANCELED','COMPLETED')),
        CONSTRAINT [CK_BookingGroups_AppointmentCounters] CHECK (
            [TotalAppointments] >= 0
            AND [ConfirmedAppointments] >= 0
            AND [CanceledAppointments] >= 0
            AND [ConfirmedAppointments] + [CanceledAppointments] <= [TotalAppointments]
        )
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_BookingGroups_GroupCode' AND object_id = OBJECT_ID(N'dbo.BookingGroups'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_BookingGroups_GroupCode] ON [dbo].[BookingGroups]([GroupCode] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BookingGroups_CustomerId_CreatedAt' AND object_id = OBJECT_ID(N'dbo.BookingGroups'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BookingGroups_CustomerId_CreatedAt]
    ON [dbo].[BookingGroups]([CustomerId] ASC, [CreatedAt] DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BookingGroups_Status_CreatedAt' AND object_id = OBJECT_ID(N'dbo.BookingGroups'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BookingGroups_Status_CreatedAt]
    ON [dbo].[BookingGroups]([Status] ASC, [CreatedAt] DESC);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BookingGroups_CustomerId'
)
BEGIN
    ALTER TABLE [dbo].[BookingGroups] WITH CHECK
    ADD CONSTRAINT [FK_BookingGroups_CustomerId]
    FOREIGN KEY([CustomerId]) REFERENCES [dbo].[Customers]([Id]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BookingGroups_BranchId'
)
BEGIN
    ALTER TABLE [dbo].[BookingGroups] WITH CHECK
    ADD CONSTRAINT [FK_BookingGroups_BranchId]
    FOREIGN KEY([BranchId]) REFERENCES [dbo].[Branches]([Id]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BookingGroups_CreatedBy'
)
BEGIN
    ALTER TABLE [dbo].[BookingGroups] WITH CHECK
    ADD CONSTRAINT [FK_BookingGroups_CreatedBy]
    FOREIGN KEY([CreatedBy]) REFERENCES [dbo].[Staffs]([Id]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BookingGroups_UpdatedBy'
)
BEGIN
    ALTER TABLE [dbo].[BookingGroups] WITH CHECK
    ADD CONSTRAINT [FK_BookingGroups_UpdatedBy]
    FOREIGN KEY([UpdatedBy]) REFERENCES [dbo].[Staffs]([Id]);
END
GO

IF COL_LENGTH('dbo.Appointments', 'BookingGroupId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Appointments]
    ADD [BookingGroupId] [bigint] NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Appointments_BookingGroupId' AND object_id = OBJECT_ID(N'dbo.Appointments'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Appointments_BookingGroupId] ON [dbo].[Appointments]([BookingGroupId] ASC);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Appointments_BookingGroupId'
)
BEGIN
    ALTER TABLE [dbo].[Appointments] WITH CHECK
    ADD CONSTRAINT [FK_Appointments_BookingGroupId]
    FOREIGN KEY([BookingGroupId]) REFERENCES [dbo].[BookingGroups]([Id]);
END
GO

