IF OBJECT_ID(N'dbo.MaintenanceReminders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MaintenanceReminders
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenanceReminders PRIMARY KEY,
        CustomerId BIGINT NOT NULL,
        VehicleId BIGINT NOT NULL,
        ServiceOrderId BIGINT NULL,
        ReminderType VARCHAR(50) NOT NULL,
        ScheduledAt DATETIME2(7) NOT NULL,
        ReminderMileage INT NULL,
        EstimatedServices NVARCHAR(1000) NULL,
        Channel VARCHAR(20) NOT NULL CONSTRAINT DF_MaintenanceReminders_Channel DEFAULT ('PUSH'),
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_MaintenanceReminders_Status DEFAULT ('PENDING'),
        SentAt DATETIME2(7) NULL,
        CustomerResponse NVARCHAR(500) NULL,
        ResultAppointmentId BIGINT NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_MaintenanceReminders_CreatedAt DEFAULT (GETDATE()),
        CreatedBy BIGINT NULL,
        UpdatedAt DATETIME2(7) NULL CONSTRAINT DF_MaintenanceReminders_UpdatedAt DEFAULT (GETDATE()),
        UpdatedBy BIGINT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MaintenanceReminders_Customers')
BEGIN
    ALTER TABLE dbo.MaintenanceReminders
    ADD CONSTRAINT FK_MaintenanceReminders_Customers
        FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MaintenanceReminders_Vehicles')
BEGIN
    ALTER TABLE dbo.MaintenanceReminders
    ADD CONSTRAINT FK_MaintenanceReminders_Vehicles
        FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(Id) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MaintenanceReminders_ServiceOrders')
BEGIN
    ALTER TABLE dbo.MaintenanceReminders
    ADD CONSTRAINT FK_MaintenanceReminders_ServiceOrders
        FOREIGN KEY (ServiceOrderId) REFERENCES dbo.ServiceOrders(Id) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MaintenanceReminders_Appointments')
BEGIN
    ALTER TABLE dbo.MaintenanceReminders
    ADD CONSTRAINT FK_MaintenanceReminders_Appointments
        FOREIGN KEY (ResultAppointmentId) REFERENCES dbo.Appointments(Id) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_MaintenanceReminders_Scheduled_Status'
      AND object_id = OBJECT_ID(N'dbo.MaintenanceReminders', N'U'))
BEGIN
    CREATE INDEX IX_MaintenanceReminders_Scheduled_Status
        ON dbo.MaintenanceReminders(ScheduledAt, Status);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_MaintenanceReminders_Dedupe'
      AND object_id = OBJECT_ID(N'dbo.MaintenanceReminders', N'U'))
BEGIN
    CREATE INDEX IX_MaintenanceReminders_Dedupe
        ON dbo.MaintenanceReminders(CustomerId, VehicleId, ReminderType, Status);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_MaintenanceReminders_Status'
      AND parent_object_id = OBJECT_ID(N'dbo.MaintenanceReminders', N'U'))
BEGIN
    ALTER TABLE dbo.MaintenanceReminders
    ADD CONSTRAINT CK_MaintenanceReminders_Status
        CHECK (Status IN ('PENDING', 'SENT', 'CUSTOMER_RESPONDED', 'BOOKED', 'DISMISSED'));
END;
GO
