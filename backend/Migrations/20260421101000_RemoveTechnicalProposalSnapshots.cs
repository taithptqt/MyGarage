using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260421101000_RemoveTechnicalProposalSnapshots")]
    public partial class RemoveTechnicalProposalSnapshots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @dropFkSql NVARCHAR(MAX) = N'';

SELECT @dropFkSql = @dropFkSql + N'ALTER TABLE dbo.Quotations DROP CONSTRAINT [' + fk.name + N'];'
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fkc.parent_object_id = OBJECT_ID(N'dbo.Quotations')
  AND c.name = N'SourceSnapshotId';

IF LEN(@dropFkSql) > 0
    EXEC sp_executesql @dropFkSql;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Quotations_SourceSnapshotId' AND object_id = OBJECT_ID(N'dbo.Quotations'))
    DROP INDEX IX_Quotations_SourceSnapshotId ON dbo.Quotations;

IF COL_LENGTH(N'dbo.Quotations', N'SourceSnapshotId') IS NOT NULL
    ALTER TABLE dbo.Quotations DROP COLUMN SourceSnapshotId;

IF OBJECT_ID(N'dbo.TechnicalProposalSnapshotDetails', N'U') IS NOT NULL
    DROP TABLE dbo.TechnicalProposalSnapshotDetails;

IF OBJECT_ID(N'dbo.TechnicalProposalSnapshots', N'U') IS NOT NULL
    DROP TABLE dbo.TechnicalProposalSnapshots;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.TechnicalProposalSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TechnicalProposalSnapshots
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TechnicalProposalSnapshots PRIMARY KEY,
        ServiceOrderId BIGINT NOT NULL,
        DiagnosisId BIGINT NOT NULL,
        VersionNo INT NOT NULL,
        Status VARCHAR(30) NOT NULL,
        LockedBy BIGINT NULL,
        LockedAt DATETIME2 NULL,
        CreatedBy BIGINT NULL,
        CreatedAt DATETIME2 NULL
    );

    CREATE INDEX IX_TechnicalProposalSnapshots_DiagnosisId ON dbo.TechnicalProposalSnapshots(DiagnosisId);
    CREATE INDEX IX_TechnicalProposalSnapshots_ServiceOrderId ON dbo.TechnicalProposalSnapshots(ServiceOrderId);
END;

IF OBJECT_ID(N'dbo.TechnicalProposalSnapshotDetails', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TechnicalProposalSnapshotDetails
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TechnicalProposalSnapshotDetails PRIMARY KEY,
        SnapshotId BIGINT NOT NULL,
        ItemType NVARCHAR(MAX) NOT NULL,
        ItemId BIGINT NULL,
        ItemName NVARCHAR(MAX) NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        IsMandatory BIT NOT NULL,
        TechnicianNotes NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NULL,
        CONSTRAINT FK_TechnicalProposalSnapshotDetails_TechnicalProposalSnapshots_SnapshotId
            FOREIGN KEY (SnapshotId) REFERENCES dbo.TechnicalProposalSnapshots(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_TechnicalProposalSnapshotDetails_SnapshotId ON dbo.TechnicalProposalSnapshotDetails(SnapshotId);
END;

IF COL_LENGTH(N'dbo.Quotations', N'SourceSnapshotId') IS NULL
    ALTER TABLE dbo.Quotations ADD SourceSnapshotId BIGINT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Quotations_SourceSnapshotId' AND object_id = OBJECT_ID(N'dbo.Quotations'))
    CREATE INDEX IX_Quotations_SourceSnapshotId ON dbo.Quotations(SourceSnapshotId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Quotations_TechnicalProposalSnapshots_SourceSnapshotId')
    ALTER TABLE dbo.Quotations
    ADD CONSTRAINT FK_Quotations_TechnicalProposalSnapshots_SourceSnapshotId
    FOREIGN KEY (SourceSnapshotId) REFERENCES dbo.TechnicalProposalSnapshots(Id);
");
        }
    }
}
