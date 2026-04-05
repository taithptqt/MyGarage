/*
    Chat V2 manual migration - ALL IN ONE (for DBeaver / SQL Server)
    - Single script to run once
    - Flow: Expand -> Backfill -> Validate -> Contract
    - If validation fails, contract phase will NOT run
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* Preflight: required legacy objects */
IF OBJECT_ID('dbo.Conversations', 'U') IS NULL
BEGIN
    RAISERROR('Missing table dbo.Conversations', 16, 1);
    RETURN;
END

IF OBJECT_ID('dbo.ChatMessages', 'U') IS NULL
BEGIN
    RAISERROR('Missing table dbo.ChatMessages', 16, 1);
    RETURN;
END

IF OBJECT_ID('dbo.Appointments', 'U') IS NULL
BEGIN
    RAISERROR('Missing table dbo.Appointments', 16, 1);
    RETURN;
END

IF OBJECT_ID('dbo.ServiceOrders', 'U') IS NULL
BEGIN
    RAISERROR('Missing table dbo.ServiceOrders', 16, 1);
    RETURN;
END

IF OBJECT_ID('dbo.Staffs', 'U') IS NULL
BEGIN
    RAISERROR('Missing table dbo.Staffs', 16, 1);
    RETURN;
END

IF OBJECT_ID('dbo.Branches', 'U') IS NULL
BEGIN
    RAISERROR('Missing table dbo.Branches', 16, 1);
    RETURN;
END

IF OBJECT_ID('dbo.Customers', 'U') IS NULL
BEGIN
    RAISERROR('Missing table dbo.Customers', 16, 1);
    RETURN;
END

IF OBJECT_ID('dbo.Vehicles', 'U') IS NULL
BEGIN
    RAISERROR('Missing table dbo.Vehicles', 16, 1);
    RETURN;
END

IF COL_LENGTH('dbo.Appointments', 'BranchId') IS NULL
BEGIN
    RAISERROR('Missing column dbo.Appointments.BranchId', 16, 1);
    RETURN;
END

IF COL_LENGTH('dbo.ServiceOrders', 'ExecuteBranchId') IS NULL
BEGIN
    RAISERROR('Missing column dbo.ServiceOrders.ExecuteBranchId', 16, 1);
    RETURN;
END

IF COL_LENGTH('dbo.Staffs', 'BranchId') IS NULL
BEGIN
    RAISERROR('Missing column dbo.Staffs.BranchId', 16, 1);
    RETURN;
END

PRINT '=== Chat V2: Phase A1 Expand START ===';

BEGIN TRY
    BEGIN TRANSACTION;

    /* 1) Expand Conversations columns */
    IF COL_LENGTH('dbo.Conversations', 'BranchId') IS NULL
        ALTER TABLE dbo.Conversations ADD BranchId BIGINT NULL;

    IF COL_LENGTH('dbo.Conversations', 'AppointmentId') IS NULL
        ALTER TABLE dbo.Conversations ADD AppointmentId BIGINT NULL;

    IF COL_LENGTH('dbo.Conversations', 'ServiceOrderId') IS NULL
        ALTER TABLE dbo.Conversations ADD ServiceOrderId BIGINT NULL;

    IF COL_LENGTH('dbo.Conversations', 'VehicleId') IS NULL
        ALTER TABLE dbo.Conversations ADD VehicleId BIGINT NULL;

    IF COL_LENGTH('dbo.Conversations', 'AssignedStaffId') IS NULL
        ALTER TABLE dbo.Conversations ADD AssignedStaffId BIGINT NULL;

    IF COL_LENGTH('dbo.Conversations', 'LastMessageId') IS NULL
        ALTER TABLE dbo.Conversations ADD LastMessageId BIGINT NULL;

    IF COL_LENGTH('dbo.Conversations', 'LastMessageAt') IS NULL
        ALTER TABLE dbo.Conversations ADD LastMessageAt DATETIME2 NULL;

    IF COL_LENGTH('dbo.Conversations', 'CreatedAt') IS NULL
        ALTER TABLE dbo.Conversations ADD CreatedAt DATETIME2 NULL;

    IF COL_LENGTH('dbo.Conversations', 'RowVersion') IS NULL
        ALTER TABLE dbo.Conversations ADD RowVersion ROWVERSION;

    /* Defaults (only if missing on column) */
    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.Conversations')
          AND c.name = 'Status'
    )
        ALTER TABLE dbo.Conversations ADD CONSTRAINT DF_Conversations_Status DEFAULT 'OPEN' FOR Status;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.Conversations')
          AND c.name = 'CreatedAt'
    )
        ALTER TABLE dbo.Conversations ADD CONSTRAINT DF_Conversations_CreatedAt DEFAULT SYSUTCDATETIME() FOR CreatedAt;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.Conversations')
          AND c.name = 'LastMessageAt'
    )
        ALTER TABLE dbo.Conversations ADD CONSTRAINT DF_Conversations_LastMessageAt DEFAULT SYSUTCDATETIME() FOR LastMessageAt;

    /* 2) Expand ChatMessages columns */
    IF COL_LENGTH('dbo.ChatMessages', 'ClientMessageId') IS NULL
        ALTER TABLE dbo.ChatMessages ADD ClientMessageId UNIQUEIDENTIFIER NULL;

    IF COL_LENGTH('dbo.ChatMessages', 'MessageType') IS NULL
        ALTER TABLE dbo.ChatMessages ADD MessageType VARCHAR(20) NULL;

    IF COL_LENGTH('dbo.ChatMessages', 'IsDeleted') IS NULL
        ALTER TABLE dbo.ChatMessages ADD IsDeleted BIT NULL;

    IF COL_LENGTH('dbo.ChatMessages', 'DeletedAt') IS NULL
        ALTER TABLE dbo.ChatMessages ADD DeletedAt DATETIME2 NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.ChatMessages')
          AND c.name = 'MessageType'
    )
        ALTER TABLE dbo.ChatMessages ADD CONSTRAINT DF_ChatMessages_MessageType DEFAULT 'TEXT' FOR MessageType;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.ChatMessages')
          AND c.name = 'IsDeleted'
    )
        ALTER TABLE dbo.ChatMessages ADD CONSTRAINT DF_ChatMessages_IsDeleted DEFAULT 0 FOR IsDeleted;

    /* 3) New read-state tables */
    IF OBJECT_ID('dbo.ConversationCustomerReads', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ConversationCustomerReads
        (
            ConversationId BIGINT NOT NULL,
            CustomerId BIGINT NOT NULL,
            LastReadMessageId BIGINT NULL,
            LastReadAt DATETIME2 NULL,
            UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ConversationCustomerReads_UpdatedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_ConversationCustomerReads PRIMARY KEY (ConversationId)
        );
    END

    IF OBJECT_ID('dbo.ConversationStaffReads', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ConversationStaffReads
        (
            ConversationId BIGINT NOT NULL,
            StaffId BIGINT NOT NULL,
            LastReadMessageId BIGINT NULL,
            LastReadAt DATETIME2 NULL,
            UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ConversationStaffReads_UpdatedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_ConversationStaffReads PRIMARY KEY (ConversationId, StaffId)
        );
    END

    /* 4) New foreign keys for Chat V2 (idempotent by name) */
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Conversations_BranchId')
        ALTER TABLE dbo.Conversations ADD CONSTRAINT FK_Conversations_BranchId
            FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Conversations_AppointmentId')
        ALTER TABLE dbo.Conversations ADD CONSTRAINT FK_Conversations_AppointmentId
            FOREIGN KEY (AppointmentId) REFERENCES dbo.Appointments(Id);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Conversations_ServiceOrderId')
        ALTER TABLE dbo.Conversations ADD CONSTRAINT FK_Conversations_ServiceOrderId
            FOREIGN KEY (ServiceOrderId) REFERENCES dbo.ServiceOrders(Id);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Conversations_VehicleId')
        ALTER TABLE dbo.Conversations ADD CONSTRAINT FK_Conversations_VehicleId
            FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(Id);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Conversations_AssignedStaffId')
        ALTER TABLE dbo.Conversations ADD CONSTRAINT FK_Conversations_AssignedStaffId
            FOREIGN KEY (AssignedStaffId) REFERENCES dbo.Staffs(Id);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Conversations_LastMessageId')
        ALTER TABLE dbo.Conversations ADD CONSTRAINT FK_Conversations_LastMessageId
            FOREIGN KEY (LastMessageId) REFERENCES dbo.ChatMessages(Id) ON DELETE SET NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ConversationCustomerReads_ConversationId')
        ALTER TABLE dbo.ConversationCustomerReads ADD CONSTRAINT FK_ConversationCustomerReads_ConversationId
            FOREIGN KEY (ConversationId) REFERENCES dbo.Conversations(Id) ON DELETE CASCADE;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ConversationCustomerReads_CustomerId')
        ALTER TABLE dbo.ConversationCustomerReads ADD CONSTRAINT FK_ConversationCustomerReads_CustomerId
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ConversationCustomerReads_LastReadMessageId')
        ALTER TABLE dbo.ConversationCustomerReads ADD CONSTRAINT FK_ConversationCustomerReads_LastReadMessageId
            FOREIGN KEY (LastReadMessageId) REFERENCES dbo.ChatMessages(Id) ON DELETE SET NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ConversationStaffReads_ConversationId')
        ALTER TABLE dbo.ConversationStaffReads ADD CONSTRAINT FK_ConversationStaffReads_ConversationId
            FOREIGN KEY (ConversationId) REFERENCES dbo.Conversations(Id) ON DELETE CASCADE;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ConversationStaffReads_StaffId')
        ALTER TABLE dbo.ConversationStaffReads ADD CONSTRAINT FK_ConversationStaffReads_StaffId
            FOREIGN KEY (StaffId) REFERENCES dbo.Staffs(Id);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ConversationStaffReads_LastReadMessageId')
        ALTER TABLE dbo.ConversationStaffReads ADD CONSTRAINT FK_ConversationStaffReads_LastReadMessageId
            FOREIGN KEY (LastReadMessageId) REFERENCES dbo.ChatMessages(Id) ON DELETE SET NULL;

    /* 5) Indexes (dynamic SQL to avoid compile-time missing-column errors) */
    /* Defensive: if script is run partially, ensure required column still exists */
    IF COL_LENGTH('dbo.ChatMessages', 'ClientMessageId') IS NULL
        ALTER TABLE dbo.ChatMessages ADD ClientMessageId UNIQUEIDENTIFIER NULL;

    IF COL_LENGTH('dbo.ChatMessages', 'ClientMessageId') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ChatMessages') AND name = 'UX_ChatMessages_ClientMessageId')
    BEGIN
        EXEC sys.sp_executesql N'
            CREATE UNIQUE INDEX UX_ChatMessages_ClientMessageId
            ON dbo.ChatMessages(ClientMessageId)
            WHERE ClientMessageId IS NOT NULL;';
    END

    IF COL_LENGTH('dbo.ChatMessages', 'ConversationId') IS NOT NULL
       AND COL_LENGTH('dbo.ChatMessages', 'Id') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ChatMessages') AND name = 'IX_ChatMessages_ConversationId_Id')
    BEGIN
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_ChatMessages_ConversationId_Id
            ON dbo.ChatMessages(ConversationId, Id);';
    END

    IF COL_LENGTH('dbo.Conversations', 'BranchId') IS NOT NULL
       AND COL_LENGTH('dbo.Conversations', 'Status') IS NOT NULL
       AND COL_LENGTH('dbo.Conversations', 'LastMessageAt') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Conversations') AND name = 'IX_Conversations_Branch_Status_LastMessageAt')
    BEGIN
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_Conversations_Branch_Status_LastMessageAt
            ON dbo.Conversations(BranchId, Status, LastMessageAt DESC);';
    END

    IF COL_LENGTH('dbo.Conversations', 'CustomerId') IS NOT NULL
       AND COL_LENGTH('dbo.Conversations', 'LastMessageAt') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Conversations') AND name = 'IX_Conversations_Customer_LastMessageAt')
    BEGIN
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_Conversations_Customer_LastMessageAt
            ON dbo.Conversations(CustomerId, LastMessageAt DESC);';
    END

    IF COL_LENGTH('dbo.ConversationStaffReads', 'StaffId') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ConversationStaffReads') AND name = 'IX_ConversationStaffReads_StaffId')
    BEGIN
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_ConversationStaffReads_StaffId
            ON dbo.ConversationStaffReads(StaffId);';
    END

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

PRINT '=== Chat V2: Phase A1 Expand DONE ===';
PRINT '=== Chat V2: Phase A2 Backfill START ===';

BEGIN TRY
    BEGIN TRANSACTION;

    /* Defensive expand for partial reruns */
    IF COL_LENGTH('dbo.ChatMessages', 'MessageType') IS NULL
        ALTER TABLE dbo.ChatMessages ADD MessageType VARCHAR(20) NULL;

    IF COL_LENGTH('dbo.ChatMessages', 'IsDeleted') IS NULL
        ALTER TABLE dbo.ChatMessages ADD IsDeleted BIT NULL;

    IF COL_LENGTH('dbo.Conversations', 'BranchId') IS NULL
        ALTER TABLE dbo.Conversations ADD BranchId BIGINT NULL;

    IF COL_LENGTH('dbo.Conversations', 'AppointmentId') IS NULL
        ALTER TABLE dbo.Conversations ADD AppointmentId BIGINT NULL;

    IF COL_LENGTH('dbo.Conversations', 'ServiceOrderId') IS NULL
        ALTER TABLE dbo.Conversations ADD ServiceOrderId BIGINT NULL;

    IF COL_LENGTH('dbo.Conversations', 'LastMessageId') IS NULL
        ALTER TABLE dbo.Conversations ADD LastMessageId BIGINT NULL;

    IF COL_LENGTH('dbo.Conversations', 'CreatedAt') IS NULL
        ALTER TABLE dbo.Conversations ADD CreatedAt DATETIME2 NULL;

    IF COL_LENGTH('dbo.Conversations', 'LastMessageAt') IS NULL
        ALTER TABLE dbo.Conversations ADD LastMessageAt DATETIME2 NULL;

    IF OBJECT_ID('dbo.ConversationCustomerReads', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ConversationCustomerReads
        (
            ConversationId BIGINT NOT NULL,
            CustomerId BIGINT NOT NULL,
            LastReadMessageId BIGINT NULL,
            LastReadAt DATETIME2 NULL,
            UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ConversationCustomerReads_UpdatedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_ConversationCustomerReads PRIMARY KEY (ConversationId)
        );
    END

    IF OBJECT_ID('dbo.ConversationStaffReads', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ConversationStaffReads
        (
            ConversationId BIGINT NOT NULL,
            StaffId BIGINT NOT NULL,
            LastReadMessageId BIGINT NULL,
            LastReadAt DATETIME2 NULL,
            UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ConversationStaffReads_UpdatedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_ConversationStaffReads PRIMARY KEY (ConversationId, StaffId)
        );
    END

    /* A) Normalize legacy values */
    UPDATE dbo.ChatMessages
    SET SenderType = 'SYSTEM'
    WHERE SenderType IS NULL
       OR SenderType NOT IN ('CUSTOMER', 'STAFF', 'SYSTEM');

    IF COL_LENGTH('dbo.ChatMessages', 'MessageType') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            UPDATE dbo.ChatMessages
            SET MessageType = ''TEXT''
            WHERE MessageType IS NULL
               OR MessageType NOT IN (''TEXT'', ''IMAGE'', ''SYSTEM'');';
    END

    IF COL_LENGTH('dbo.ChatMessages', 'IsDeleted') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            UPDATE dbo.ChatMessages
            SET IsDeleted = 0
            WHERE IsDeleted IS NULL;';
    END

    UPDATE dbo.Conversations
    SET Status = 'OPEN'
    WHERE Status IS NULL
       OR Status NOT IN ('OPEN', 'WAITING_FOR_STAFF', 'WAITING_FOR_CUSTOMER', 'CLOSED');

    /* B) Backfill BranchId priority: Appointment -> ServiceOrder -> Staff */
    IF COL_LENGTH('dbo.Conversations', 'BranchId') IS NOT NULL
       AND COL_LENGTH('dbo.Conversations', 'AppointmentId') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            UPDATE c
            SET c.BranchId = a.BranchId
            FROM dbo.Conversations c
            JOIN dbo.Appointments a ON a.Id = c.AppointmentId
            WHERE c.BranchId IS NULL
              AND c.AppointmentId IS NOT NULL
              AND a.BranchId IS NOT NULL;';
    END

    IF COL_LENGTH('dbo.Conversations', 'BranchId') IS NOT NULL
       AND COL_LENGTH('dbo.Conversations', 'ServiceOrderId') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            UPDATE c
            SET c.BranchId = so.ExecuteBranchId
            FROM dbo.Conversations c
            JOIN dbo.ServiceOrders so ON so.Id = c.ServiceOrderId
            WHERE c.BranchId IS NULL
              AND c.ServiceOrderId IS NOT NULL
              AND so.ExecuteBranchId IS NOT NULL;';
    END

    IF COL_LENGTH('dbo.Conversations', 'BranchId') IS NOT NULL
       AND COL_LENGTH('dbo.Conversations', 'StaffId') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            UPDATE c
            SET c.BranchId = s.BranchId
            FROM dbo.Conversations c
            JOIN dbo.Staffs s ON s.Id = c.StaffId
            WHERE c.BranchId IS NULL
              AND c.StaffId IS NOT NULL
              AND s.BranchId IS NOT NULL;';
    END

    /* C) Backfill conversation timeline and last message pointers */
    IF COL_LENGTH('dbo.Conversations', 'LastMessageId') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            ;WITH LastMessageCte AS
            (
                SELECT
                    m.ConversationId,
                    MAX(m.Id) AS LastMessageId
                FROM dbo.ChatMessages m
                GROUP BY m.ConversationId
            )
            UPDATE c
            SET c.LastMessageId = lm.LastMessageId
            FROM dbo.Conversations c
            JOIN LastMessageCte lm ON lm.ConversationId = c.Id
            WHERE c.LastMessageId IS NULL;';
    END

    IF COL_LENGTH('dbo.Conversations', 'CreatedAt') IS NOT NULL
       AND COL_LENGTH('dbo.Conversations', 'LastMessageAt') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            UPDATE c
            SET
                c.CreatedAt = COALESCE(c.CreatedAt, c.UpdatedAt, msg.FirstCreatedAt, SYSUTCDATETIME()),
                c.LastMessageAt = COALESCE(c.LastMessageAt, msg.LastCreatedAt, c.UpdatedAt, c.CreatedAt, SYSUTCDATETIME())
            FROM dbo.Conversations c
            OUTER APPLY
            (
                SELECT
                    MIN(m.CreatedAt) AS FirstCreatedAt,
                    MAX(m.CreatedAt) AS LastCreatedAt
                FROM dbo.ChatMessages m
                WHERE m.ConversationId = c.Id
            ) msg;';
    END

    /* D) Seed read-state tables */
    IF OBJECT_ID('dbo.ConversationCustomerReads', 'U') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.ConversationCustomerReads (ConversationId, CustomerId, LastReadMessageId, LastReadAt, UpdatedAt)
            SELECT
                c.Id,
                c.CustomerId,
                lm.LastMessageId,
                lm.LastMessageAt,
                SYSUTCDATETIME()
            FROM dbo.Conversations c
            OUTER APPLY
            (
                SELECT TOP (1)
                    m.Id AS LastMessageId,
                    m.CreatedAt AS LastMessageAt
                FROM dbo.ChatMessages m
                WHERE m.ConversationId = c.Id
                ORDER BY m.Id DESC
            ) lm
            LEFT JOIN dbo.ConversationCustomerReads r ON r.ConversationId = c.Id
            WHERE r.ConversationId IS NULL;';
    END

    IF OBJECT_ID('dbo.ConversationStaffReads', 'U') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.ConversationStaffReads (ConversationId, StaffId, LastReadMessageId, LastReadAt, UpdatedAt)
            SELECT
                c.Id,
                c.StaffId,
                lm.LastMessageId,
                lm.LastMessageAt,
                SYSUTCDATETIME()
            FROM dbo.Conversations c
            OUTER APPLY
            (
                SELECT TOP (1)
                    m.Id AS LastMessageId,
                    m.CreatedAt AS LastMessageAt
                FROM dbo.ChatMessages m
                WHERE m.ConversationId = c.Id
                ORDER BY m.Id DESC
            ) lm
            LEFT JOIN dbo.ConversationStaffReads sr
                ON sr.ConversationId = c.Id
               AND sr.StaffId = c.StaffId
            WHERE c.StaffId IS NOT NULL
              AND sr.ConversationId IS NULL;';
    END

    /* E) Replace legacy check constraints with Chat V2 constraints */
    DECLARE @dropSql NVARCHAR(MAX) = N'';

    SELECT @dropSql = @dropSql + N'ALTER TABLE dbo.Conversations DROP CONSTRAINT [' + cc.name + N'];' + CHAR(10)
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID('dbo.Conversations')
      AND (cc.name = 'CK_Conversations_Status' OR cc.name LIKE 'CK__Conversat__Statu%');

    IF LEN(@dropSql) > 0
        EXEC sys.sp_executesql @dropSql;

    SET @dropSql = N'';
    SELECT @dropSql = @dropSql + N'ALTER TABLE dbo.ChatMessages DROP CONSTRAINT [' + cc.name + N'];' + CHAR(10)
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID('dbo.ChatMessages')
      AND (
            cc.name = 'CK_ChatMessages_SenderType'
         OR cc.name = 'CK_ChatMessages_MessageType'
         OR cc.name LIKE 'CK__ChatMessa__Sende%'
         OR cc.name LIKE 'CK__ChatMessa__Messa%'
      );

    IF LEN(@dropSql) > 0
        EXEC sys.sp_executesql @dropSql;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.Conversations') AND name = 'CK_Conversations_Status')
    BEGIN
        ALTER TABLE dbo.Conversations WITH CHECK
        ADD CONSTRAINT CK_Conversations_Status
        CHECK ([Status] IN ('OPEN', 'WAITING_FOR_STAFF', 'WAITING_FOR_CUSTOMER', 'CLOSED'));
    END

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.ChatMessages') AND name = 'CK_ChatMessages_SenderType')
    BEGIN
        ALTER TABLE dbo.ChatMessages WITH CHECK
        ADD CONSTRAINT CK_ChatMessages_SenderType
        CHECK ([SenderType] IN ('CUSTOMER', 'STAFF', 'SYSTEM'));
    END

    IF COL_LENGTH('dbo.ChatMessages', 'MessageType') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.ChatMessages') AND name = 'CK_ChatMessages_MessageType')
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.ChatMessages WITH CHECK
            ADD CONSTRAINT CK_ChatMessages_MessageType
            CHECK ([MessageType] IN (''TEXT'', ''IMAGE'', ''SYSTEM''));';
    END

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

PRINT '=== Chat V2: Phase A2 Backfill DONE ===';
PRINT '=== Chat V2: Validation START ===';

DECLARE @ConversationsWithoutBranchId BIGINT;
DECLARE @InvalidConversationStatus BIGINT;
DECLARE @InvalidSenderType BIGINT;
DECLARE @InvalidMessageType BIGINT;

SET @ConversationsWithoutBranchId = 0;
IF COL_LENGTH('dbo.Conversations', 'BranchId') IS NULL
    SET @ConversationsWithoutBranchId = 1;
ELSE
    EXEC sys.sp_executesql
        N'SELECT @cntOut = COUNT(1) FROM dbo.Conversations WHERE BranchId IS NULL;',
        N'@cntOut BIGINT OUTPUT',
        @cntOut = @ConversationsWithoutBranchId OUTPUT;

SELECT @InvalidConversationStatus = COUNT(1)
FROM dbo.Conversations
WHERE Status IS NULL
   OR Status NOT IN ('OPEN', 'WAITING_FOR_STAFF', 'WAITING_FOR_CUSTOMER', 'CLOSED');

SELECT @InvalidSenderType = COUNT(1)
FROM dbo.ChatMessages
WHERE SenderType NOT IN ('CUSTOMER', 'STAFF', 'SYSTEM');

SET @InvalidMessageType = 0;
IF COL_LENGTH('dbo.ChatMessages', 'MessageType') IS NULL
    SET @InvalidMessageType = 1;
ELSE
    EXEC sys.sp_executesql
        N'SELECT @cntOut = COUNT(1) FROM dbo.ChatMessages WHERE MessageType IS NULL OR MessageType NOT IN (''TEXT'', ''IMAGE'', ''SYSTEM'');',
        N'@cntOut BIGINT OUTPUT',
        @cntOut = @InvalidMessageType OUTPUT;

SELECT
    @ConversationsWithoutBranchId AS ConversationsWithoutBranchId,
    @InvalidConversationStatus AS InvalidConversationStatus,
    @InvalidSenderType AS InvalidSenderType,
    @InvalidMessageType AS InvalidMessageType;

IF (@ConversationsWithoutBranchId > 0 OR @InvalidConversationStatus > 0 OR @InvalidSenderType > 0 OR @InvalidMessageType > 0)
BEGIN
    PRINT 'Validation failed. Contract phase skipped.';
    IF COL_LENGTH('dbo.Conversations', 'BranchId') IS NOT NULL
       AND COL_LENGTH('dbo.Conversations', 'AppointmentId') IS NOT NULL
       AND COL_LENGTH('dbo.Conversations', 'ServiceOrderId') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            SELECT
                c.Id,
                c.CustomerId,
                c.StaffId,
                c.AppointmentId,
                c.ServiceOrderId
            FROM dbo.Conversations c
            WHERE c.BranchId IS NULL
            ORDER BY c.Id;';
    END

    RAISERROR('STOP: Chat V2 validation failed. Fix data and rerun script.', 16, 1);
    RETURN;
END

PRINT '=== Chat V2: Validation PASSED ===';
PRINT '=== Chat V2: Phase B Contract START ===';

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH('dbo.ChatMessages', 'MessageType') IS NULL
        ALTER TABLE dbo.ChatMessages ADD MessageType VARCHAR(20) NULL;

    IF COL_LENGTH('dbo.ChatMessages', 'IsDeleted') IS NULL
        ALTER TABLE dbo.ChatMessages ADD IsDeleted BIT NULL;

    EXEC sys.sp_executesql N'
        UPDATE dbo.ChatMessages
        SET MessageType = ''TEXT''
        WHERE MessageType IS NULL;';

    EXEC sys.sp_executesql N'
        UPDATE dbo.ChatMessages
        SET IsDeleted = 0
        WHERE IsDeleted IS NULL;';

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.Conversations')
          AND name = 'BranchId'
          AND is_nullable = 1
    )
    BEGIN
        DECLARE @RecreateIx_Conversations_Branch_Status_LastMessageAt BIT = 0;

        IF EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE object_id = OBJECT_ID('dbo.Conversations')
              AND name = 'IX_Conversations_Branch_Status_LastMessageAt'
        )
        BEGIN
            SET @RecreateIx_Conversations_Branch_Status_LastMessageAt = 1;
            EXEC sys.sp_executesql N'DROP INDEX IX_Conversations_Branch_Status_LastMessageAt ON dbo.Conversations;';
        END

        EXEC sys.sp_executesql N'ALTER TABLE dbo.Conversations ALTER COLUMN BranchId BIGINT NOT NULL;';

        IF @RecreateIx_Conversations_Branch_Status_LastMessageAt = 1
           AND COL_LENGTH('dbo.Conversations', 'BranchId') IS NOT NULL
           AND COL_LENGTH('dbo.Conversations', 'Status') IS NOT NULL
           AND COL_LENGTH('dbo.Conversations', 'LastMessageAt') IS NOT NULL
           AND NOT EXISTS (
               SELECT 1
               FROM sys.indexes
               WHERE object_id = OBJECT_ID('dbo.Conversations')
                 AND name = 'IX_Conversations_Branch_Status_LastMessageAt'
           )
        BEGIN
            EXEC sys.sp_executesql N'
                CREATE INDEX IX_Conversations_Branch_Status_LastMessageAt
                ON dbo.Conversations(BranchId, Status, LastMessageAt DESC);';
        END
    END

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.ChatMessages')
        AND name = 'MessageType'
        AND is_nullable = 1
    )
        EXEC sys.sp_executesql N'ALTER TABLE dbo.ChatMessages ALTER COLUMN MessageType VARCHAR(20) NOT NULL;';

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.ChatMessages')
        AND name = 'IsDeleted'
        AND is_nullable = 1
    )
        EXEC sys.sp_executesql N'ALTER TABLE dbo.ChatMessages ALTER COLUMN IsDeleted BIT NOT NULL;';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

PRINT '=== Chat V2: Phase B Contract DONE ===';
PRINT '=== Chat V2 all-in-one migration completed successfully ===';
