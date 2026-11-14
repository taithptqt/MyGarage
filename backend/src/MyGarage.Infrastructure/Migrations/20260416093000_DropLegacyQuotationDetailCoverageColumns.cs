using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260416093000_DropLegacyQuotationDetailCoverageColumns")]
    public partial class DropLegacyQuotationDetailCoverageColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_QuotationDetails_CoverageSum')
BEGIN
    ALTER TABLE dbo.QuotationDetails DROP CONSTRAINT CK_QuotationDetails_CoverageSum;
END");

            migrationBuilder.DropColumn(
                name: "InsuranceRequestedAmount",
                table: "QuotationDetails");

            migrationBuilder.DropColumn(
                name: "CustomerCoPayAmount",
                table: "QuotationDetails");

            migrationBuilder.Sql(@"
ALTER TABLE dbo.QuotationDetails
ADD CONSTRAINT CK_QuotationDetails_CoverageSum CHECK (
    ABS(
        ISNULL([InsuranceCoveredAmount], 0) +
        ISNULL([CustomerCoveredAmount], 0) -
        ISNULL([FinalAmount], 0)
    ) <= 0.01
)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_QuotationDetails_CoverageSum')
BEGIN
    ALTER TABLE dbo.QuotationDetails DROP CONSTRAINT CK_QuotationDetails_CoverageSum;
END");

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceRequestedAmount",
                table: "QuotationDetails",
                type: "decimal(18, 2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerCoPayAmount",
                table: "QuotationDetails",
                type: "decimal(18, 2)",
                nullable: true);

            migrationBuilder.Sql(@"
ALTER TABLE dbo.QuotationDetails
ADD CONSTRAINT CK_QuotationDetails_CoverageSum CHECK (
    ABS(
        ISNULL([InsuranceCoveredAmount], ISNULL([InsuranceRequestedAmount], 0)) +
        ISNULL([CustomerCoveredAmount], ISNULL([CustomerCoPayAmount], 0)) -
        ISNULL([FinalAmount], 0)
    ) <= 0.01
)");
        }
    }
}
