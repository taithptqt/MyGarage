using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260417014000_AddInsuranceChangeTypeAuditToQuotations")]
    public partial class AddInsuranceChangeTypeAuditToQuotations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InsuranceChangeType",
                table: "Quotations",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "NONE");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_InsuranceChangeType",
                table: "Quotations",
                column: "InsuranceChangeType");

            migrationBuilder.Sql(@"
UPDATE q
SET q.InsuranceChangeType =
    CASE
        WHEN src.PrevInsuranceAmount IS NULL THEN CASE WHEN q.TotalInsuranceAmount > 0 THEN 'INCREASE' ELSE 'NONE' END
        WHEN q.TotalInsuranceAmount > src.PrevInsuranceAmount THEN 'INCREASE'
        WHEN q.TotalInsuranceAmount < src.PrevInsuranceAmount THEN 'DECREASE'
        ELSE 'NONE'
    END
FROM Quotations q
INNER JOIN (
    SELECT
        Id,
        LAG(TotalInsuranceAmount) OVER (PARTITION BY ServiceOrderId ORDER BY VersionNo, Id) AS PrevInsuranceAmount
    FROM Quotations
) src ON src.Id = q.Id;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Quotations_InsuranceApprovalEvidence_Required')
BEGIN
    ALTER TABLE [Quotations] DROP CONSTRAINT [CK_Quotations_InsuranceApprovalEvidence_Required];
END
");

            migrationBuilder.Sql(@"
ALTER TABLE [Quotations] WITH CHECK ADD CONSTRAINT [CK_Quotations_InsuranceApprovalEvidence_Required]
CHECK (
    [Status] <> 'INSURANCE_APPROVED'
    OR [TotalInsuranceAmount] <= 0
    OR NULLIF(LTRIM(RTRIM([InsuranceApprovalEvidenceUrl])), '') IS NOT NULL
);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Quotations_InsuranceApprovalEvidence_Required')
BEGIN
    ALTER TABLE [Quotations] DROP CONSTRAINT [CK_Quotations_InsuranceApprovalEvidence_Required];
END
");

            migrationBuilder.Sql(@"
ALTER TABLE [Quotations] WITH CHECK ADD CONSTRAINT [CK_Quotations_InsuranceApprovalEvidence_Required]
CHECK (
    [Status] <> 'INSURANCE_APPROVED'
    OR NULLIF(LTRIM(RTRIM([InsuranceApprovalEvidenceUrl])), '') IS NOT NULL
);
");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_InsuranceChangeType",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "InsuranceChangeType",
                table: "Quotations");
        }
    }
}
