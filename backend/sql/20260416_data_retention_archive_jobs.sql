-- Data retention + archive policy for high-volume tables
-- Scope: chat_messages, stock_transactions, audit_logs, maintenance_reminders

SET XACT_ABORT ON;
GO

IF OBJECT_ID('dbo.chat_messages', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.chat_messages_archive', 'U') IS NULL
BEGIN
    SELECT TOP 0 * INTO dbo.chat_messages_archive FROM dbo.chat_messages;
    CREATE INDEX IX_chat_messages_archive_created_at ON dbo.chat_messages_archive(created_at);
END
GO

IF OBJECT_ID('dbo.stock_transactions', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.stock_transactions_archive', 'U') IS NULL
BEGIN
    SELECT TOP 0 * INTO dbo.stock_transactions_archive FROM dbo.stock_transactions;
    CREATE INDEX IX_stock_transactions_archive_created_at ON dbo.stock_transactions_archive(created_at);
END
GO

IF OBJECT_ID('dbo.audit_logs', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.audit_logs_archive', 'U') IS NULL
BEGIN
    SELECT TOP 0 * INTO dbo.audit_logs_archive FROM dbo.audit_logs;
    CREATE INDEX IX_audit_logs_archive_timestamp ON dbo.audit_logs_archive([timestamp]);
END
GO

IF OBJECT_ID('dbo.maintenance_reminders', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.maintenance_reminders_archive', 'U') IS NULL
BEGIN
    SELECT TOP 0 * INTO dbo.maintenance_reminders_archive FROM dbo.maintenance_reminders;
    CREATE INDEX IX_maintenance_reminders_archive_created_at ON dbo.maintenance_reminders_archive(created_at);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_archive_operational_logs
    @chat_days INT = 365,
    @stock_days INT = 730,
    @audit_days INT = 730,
    @reminder_days INT = 365,
    @batch_size INT = 5000
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @now DATETIME2 = SYSDATETIME();

    IF OBJECT_ID('dbo.chat_messages', 'U') IS NOT NULL
       AND OBJECT_ID('dbo.chat_messages_archive', 'U') IS NOT NULL
    BEGIN
        WHILE 1 = 1
        BEGIN
            ;WITH src AS (
                SELECT TOP (@batch_size) *
                FROM dbo.chat_messages
                WHERE created_at < DATEADD(DAY, -@chat_days, @now)
                ORDER BY id
            )
            DELETE FROM src
            OUTPUT deleted.* INTO dbo.chat_messages_archive;

            IF @@ROWCOUNT = 0 BREAK;
        END
    END

    IF OBJECT_ID('dbo.stock_transactions', 'U') IS NOT NULL
       AND OBJECT_ID('dbo.stock_transactions_archive', 'U') IS NOT NULL
    BEGIN
        WHILE 1 = 1
        BEGIN
            ;WITH src AS (
                SELECT TOP (@batch_size) *
                FROM dbo.stock_transactions
                WHERE created_at < DATEADD(DAY, -@stock_days, @now)
                ORDER BY id
            )
            DELETE FROM src
            OUTPUT deleted.* INTO dbo.stock_transactions_archive;

            IF @@ROWCOUNT = 0 BREAK;
        END
    END

    IF OBJECT_ID('dbo.audit_logs', 'U') IS NOT NULL
       AND OBJECT_ID('dbo.audit_logs_archive', 'U') IS NOT NULL
    BEGIN
        WHILE 1 = 1
        BEGIN
            ;WITH src AS (
                SELECT TOP (@batch_size) *
                FROM dbo.audit_logs
                WHERE [timestamp] < DATEADD(DAY, -@audit_days, @now)
                ORDER BY id
            )
            DELETE FROM src
            OUTPUT deleted.* INTO dbo.audit_logs_archive;

            IF @@ROWCOUNT = 0 BREAK;
        END
    END

    IF OBJECT_ID('dbo.maintenance_reminders', 'U') IS NOT NULL
       AND OBJECT_ID('dbo.maintenance_reminders_archive', 'U') IS NOT NULL
    BEGIN
        WHILE 1 = 1
        BEGIN
            ;WITH src AS (
                SELECT TOP (@batch_size) *
                FROM dbo.maintenance_reminders
                WHERE created_at < DATEADD(DAY, -@reminder_days, @now)
                ORDER BY id
            )
            DELETE FROM src
            OUTPUT deleted.* INTO dbo.maintenance_reminders_archive;

            IF @@ROWCOUNT = 0 BREAK;
        END
    END
END
GO

-- Schedule recommendation:
-- EXEC dbo.sp_archive_operational_logs;
