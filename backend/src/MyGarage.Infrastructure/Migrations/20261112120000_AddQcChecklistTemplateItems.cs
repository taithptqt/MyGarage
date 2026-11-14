using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20261112120000_AddQcChecklistTemplateItems")]
    public partial class AddQcChecklistTemplateItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
IF COL_LENGTH(N'dbo.QcChecklistTemplates', N'Description') IS NULL
BEGIN
    ALTER TABLE dbo.QcChecklistTemplates
    ADD Description NVARCHAR(1000) NULL;
END

IF OBJECT_ID(N'dbo.QcChecklistTemplateItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QcChecklistTemplateItems
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_QcChecklistTemplateItems PRIMARY KEY,
        TemplateId BIGINT NOT NULL,
        Category NVARCHAR(100) NULL,
        ItemName NVARCHAR(255) NOT NULL,
        Description NVARCHAR(1000) NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_QcChecklistTemplateItems_IsRequired DEFAULT (1),
        SortOrder INT NOT NULL CONSTRAINT DF_QcChecklistTemplateItems_SortOrder DEFAULT (0),
        CreatedAt DATETIME2 NULL CONSTRAINT DF_QcChecklistTemplateItems_CreatedAt DEFAULT (GETDATE()),
        CONSTRAINT FK_QcChecklistTemplateItems_QcChecklistTemplates_TemplateId
            FOREIGN KEY (TemplateId) REFERENCES dbo.QcChecklistTemplates(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_QcChecklistTemplateItems_TemplateId_SortOrder
        ON dbo.QcChecklistTemplateItems(TemplateId, SortOrder);
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
IF OBJECT_ID(N'dbo.QcChecklistTemplateItems', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.QcChecklistTemplateItems;
END

IF COL_LENGTH(N'dbo.QcChecklistTemplates', N'Description') IS NOT NULL
BEGIN
    ALTER TABLE dbo.QcChecklistTemplates
    DROP COLUMN Description;
END
""");
        }
    }
}
