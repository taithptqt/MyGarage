using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260421113000_DropQuotationTotalDiscount")]
    public partial class DropQuotationTotalDiscount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Quotations', N'TotalDiscount') IS NOT NULL
BEGIN
    DECLARE @dropDefaultConstraintSql NVARCHAR(MAX) = N'';

    SELECT @dropDefaultConstraintSql = N'ALTER TABLE dbo.Quotations DROP CONSTRAINT [' + dc.name + N'];'
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Quotations')
      AND c.name = N'TotalDiscount';

    IF LEN(@dropDefaultConstraintSql) > 0
        EXEC sp_executesql @dropDefaultConstraintSql;

    ALTER TABLE dbo.Quotations DROP COLUMN TotalDiscount;
END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Quotations', N'TotalDiscount') IS NULL
BEGIN
    ALTER TABLE dbo.Quotations
    ADD TotalDiscount DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_Quotations_TotalDiscount DEFAULT (0);
END");
        }
    }
}
