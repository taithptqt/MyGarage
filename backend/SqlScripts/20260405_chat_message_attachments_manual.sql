/*
    Chat message attachments (multi-image) - manual SQL
    - Purpose: add support many images per chat message
    - Mode: manual run in DBeaver / SQL Server
    - Safe: idempotent, no destructive drop/reset
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* Preflight */
IF OBJECT_ID('dbo.ChatMessages', 'U') IS NULL
BEGIN
    RAISERROR('Missing table dbo.ChatMessages', 16, 1);
    RETURN;
END

IF COL_LENGTH('dbo.ChatMessages', 'Id') IS NULL
BEGIN
    RAISERROR('Missing column dbo.ChatMessages.Id', 16, 1);
    RETURN;
END

PRINT '=== Chat Attachments: Phase A1 Expand START ===';

BEGIN TRY
    BEGIN TRANSACTION;

    /* 1) Create table if absent */
    IF OBJECT_ID('dbo.ChatMessageAttachments', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ChatMessageAttachments (
            Id BIGINT IDENTITY(1,1) NOT NULL,
            MessageId BIGINT NOT NULL,
            AttachmentUrl VARCHAR(500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
            ContentType VARCHAR(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
            FileSize BIGINT NULL,
            SortOrder INT NOT NULL,
            CreatedAt DATETIME2 NOT NULL,
            CONSTRAINT PK_ChatMessageAttachments PRIMARY KEY (Id)
        );
    END

    /* 2) Expand columns for existing table (if table was created manually before) */
    IF COL_LENGTH('dbo.ChatMessageAttachments', 'MessageId') IS NULL
        ALTER TABLE dbo.ChatMessageAttachments ADD MessageId BIGINT NOT NULL;

    IF COL_LENGTH('dbo.ChatMessageAttachments', 'AttachmentUrl') IS NULL
        ALTER TABLE dbo.ChatMessageAttachments ADD AttachmentUrl VARCHAR(500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL CONSTRAINT DF_ChatMessageAttachments_AttachmentUrl DEFAULT ('');

    IF COL_LENGTH('dbo.ChatMessageAttachments', 'ContentType') IS NULL
        ALTER TABLE dbo.ChatMessageAttachments ADD ContentType VARCHAR(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL;

    IF COL_LENGTH('dbo.ChatMessageAttachments', 'FileSize') IS NULL
        ALTER TABLE dbo.ChatMessageAttachments ADD FileSize BIGINT NULL;

    IF COL_LENGTH('dbo.ChatMessageAttachments', 'SortOrder') IS NULL
        ALTER TABLE dbo.ChatMessageAttachments ADD SortOrder INT NOT NULL CONSTRAINT DF_ChatMessageAttachments_SortOrder DEFAULT (1);

    IF COL_LENGTH('dbo.ChatMessageAttachments', 'CreatedAt') IS NULL
        ALTER TABLE dbo.ChatMessageAttachments ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ChatMessageAttachments_CreatedAt DEFAULT (SYSUTCDATETIME());

    /* 3) Ensure defaults for SortOrder / CreatedAt */
    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.ChatMessageAttachments')
          AND c.name = 'SortOrder'
    )
    BEGIN
        ALTER TABLE dbo.ChatMessageAttachments
        ADD CONSTRAINT DF_ChatMessageAttachments_SortOrder DEFAULT (1) FOR SortOrder;
    END

    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.ChatMessageAttachments')
          AND c.name = 'CreatedAt'
    )
    BEGIN
        ALTER TABLE dbo.ChatMessageAttachments
        ADD CONSTRAINT DF_ChatMessageAttachments_CreatedAt DEFAULT (SYSUTCDATETIME()) FOR CreatedAt;
    END

    /* 4) FK to ChatMessages(Id) */
    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_key_columns fkc
        JOIN sys.tables t ON t.object_id = fkc.parent_object_id
        JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = fkc.parent_column_id
        JOIN sys.tables rt ON rt.object_id = fkc.referenced_object_id
        JOIN sys.columns rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
        WHERE SCHEMA_NAME(t.schema_id) = 'dbo'
          AND t.name = 'ChatMessageAttachments'
          AND c.name = 'MessageId'
          AND SCHEMA_NAME(rt.schema_id) = 'dbo'
          AND rt.name = 'ChatMessages'
          AND rc.name = 'Id'
    )
    BEGIN
        ALTER TABLE dbo.ChatMessageAttachments
        ADD CONSTRAINT FK_ChatMessageAttachments_MessageId
        FOREIGN KEY (MessageId) REFERENCES dbo.ChatMessages(Id) ON DELETE CASCADE;
    END

    /* 5) Optional data-quality check */
    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID('dbo.ChatMessageAttachments')
          AND name = 'CK_ChatMessageAttachments_SortOrder'
    )
    BEGIN
        ALTER TABLE dbo.ChatMessageAttachments
        ADD CONSTRAINT CK_ChatMessageAttachments_SortOrder CHECK (SortOrder >= 1);
    END

    /* 6) Indexes */
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.ChatMessageAttachments')
          AND name = 'IX_ChatMessageAttachments_MessageId_SortOrder_Id'
    )
    BEGIN
        CREATE NONCLUSTERED INDEX IX_ChatMessageAttachments_MessageId_SortOrder_Id
        ON dbo.ChatMessageAttachments (MessageId ASC, SortOrder ASC, Id ASC);
    END

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.ChatMessageAttachments')
          AND name = 'IX_ChatMessageAttachments_CreatedAt'
    )
    BEGIN
        CREATE NONCLUSTERED INDEX IX_ChatMessageAttachments_CreatedAt
        ON dbo.ChatMessageAttachments (CreatedAt DESC);
    END

    COMMIT TRANSACTION;
    PRINT '=== Chat Attachments: Phase A1 Expand DONE ===';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

PRINT '=== Chat Attachments: Phase A2 Backfill START ===';

BEGIN TRY
    BEGIN TRANSACTION;

    /* Backfill legacy single AttachmentUrl -> attachment row (SortOrder = 1) */
    IF COL_LENGTH('dbo.ChatMessages', 'AttachmentUrl') IS NOT NULL
    BEGIN
        ;WITH Legacy AS (
            SELECT
                m.Id AS MessageId,
                LTRIM(RTRIM(m.AttachmentUrl)) AS AttachmentUrl,
                m.CreatedAt
            FROM dbo.ChatMessages m
            WHERE m.AttachmentUrl IS NOT NULL
              AND LTRIM(RTRIM(m.AttachmentUrl)) <> ''
        )
        INSERT INTO dbo.ChatMessageAttachments
        (
            MessageId,
            AttachmentUrl,
            ContentType,
            FileSize,
            SortOrder,
            CreatedAt
        )
        SELECT
            l.MessageId,
            l.AttachmentUrl,
            CASE
                WHEN LOWER(l.AttachmentUrl) LIKE '%.png' OR LOWER(l.AttachmentUrl) LIKE '%.png?%' THEN 'image/png'
                WHEN LOWER(l.AttachmentUrl) LIKE '%.jpg' OR LOWER(l.AttachmentUrl) LIKE '%.jpg?%' THEN 'image/jpeg'
                WHEN LOWER(l.AttachmentUrl) LIKE '%.jpeg' OR LOWER(l.AttachmentUrl) LIKE '%.jpeg?%' THEN 'image/jpeg'
                WHEN LOWER(l.AttachmentUrl) LIKE '%.webp' OR LOWER(l.AttachmentUrl) LIKE '%.webp?%' THEN 'image/webp'
                WHEN LOWER(l.AttachmentUrl) LIKE '%.gif' OR LOWER(l.AttachmentUrl) LIKE '%.gif?%' THEN 'image/gif'
                WHEN LOWER(l.AttachmentUrl) LIKE '%.bmp' OR LOWER(l.AttachmentUrl) LIKE '%.bmp?%' THEN 'image/bmp'
                ELSE 'image/*'
            END,
            NULL,
            1,
            COALESCE(l.CreatedAt, SYSUTCDATETIME())
        FROM Legacy l
        WHERE NOT EXISTS (
            SELECT 1
            FROM dbo.ChatMessageAttachments a
            WHERE a.MessageId = l.MessageId
        );
    END

    COMMIT TRANSACTION;
    PRINT '=== Chat Attachments: Phase A2 Backfill DONE ===';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

PRINT '=== Chat Attachments: Phase A3 Validate START ===';

DECLARE @legacyMessageWithAttachment BIGINT = 0;
DECLARE @mappedLegacyMessage BIGINT = 0;

SELECT @legacyMessageWithAttachment = COUNT(1)
FROM dbo.ChatMessages m
WHERE m.AttachmentUrl IS NOT NULL
  AND LTRIM(RTRIM(m.AttachmentUrl)) <> '';

SELECT @mappedLegacyMessage = COUNT(1)
FROM dbo.ChatMessages m
WHERE m.AttachmentUrl IS NOT NULL
  AND LTRIM(RTRIM(m.AttachmentUrl)) <> ''
  AND EXISTS (
      SELECT 1
      FROM dbo.ChatMessageAttachments a
      WHERE a.MessageId = m.Id
  );

SELECT
    @legacyMessageWithAttachment AS LegacyMessagesWithAttachmentUrl,
    @mappedLegacyMessage AS LegacyMessagesMappedToAttachmentTable;

IF (@mappedLegacyMessage < @legacyMessageWithAttachment)
BEGIN
    RAISERROR(
        'Validation failed: only %d/%d legacy messages were mapped to dbo.ChatMessageAttachments.',
        16, 1,
        @mappedLegacyMessage, @legacyMessageWithAttachment
    );
    RETURN;
END

PRINT '=== Chat Attachments: Phase A3 Validate PASSED ===';
PRINT '=== Chat Attachments: COMPLETED ===';
