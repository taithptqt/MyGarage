IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens
    (
        Id BIGINT IDENTITY(1,1) NOT NULL,
        CustomerId BIGINT NULL,
        StaffId BIGINT NULL,
        TokenHash VARCHAR(64) NOT NULL,
        ExpiresAt DATETIME2 NOT NULL,
        UsedAt DATETIME2 NULL,
        RevokedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_PasswordResetTokens_CreatedAt DEFAULT (sysutcdatetime()),
        CONSTRAINT PK_PasswordResetTokens PRIMARY KEY (Id),
        CONSTRAINT CK_PasswordResetTokens_Principal CHECK (
            (CustomerId IS NOT NULL AND StaffId IS NULL) OR
            (CustomerId IS NULL AND StaffId IS NOT NULL)
        ),
        CONSTRAINT CK_PasswordResetTokens_TokenHash_NotEmpty CHECK (LEN(TokenHash) > 0),
        CONSTRAINT FK_PasswordResetTokens_Customers_CustomerId
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id) ON DELETE CASCADE,
        CONSTRAINT FK_PasswordResetTokens_Staffs_StaffId
            FOREIGN KEY (StaffId) REFERENCES dbo.Staffs (Id) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX UX_PasswordResetTokens_TokenHash
        ON dbo.PasswordResetTokens (TokenHash);

    CREATE INDEX IX_PasswordResetTokens_CustomerId_ExpiresAt
        ON dbo.PasswordResetTokens (CustomerId, ExpiresAt);

    CREATE INDEX IX_PasswordResetTokens_StaffId_ExpiresAt
        ON dbo.PasswordResetTokens (StaffId, ExpiresAt);
END;
