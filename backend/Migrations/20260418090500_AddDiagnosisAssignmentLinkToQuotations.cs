using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260418090500_AddDiagnosisAssignmentLinkToQuotations")]
    public partial class AddDiagnosisAssignmentLinkToQuotations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DiagnosisAssignmentId",
                table: "Quotations",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(@"
;WITH AssignmentCandidates AS (
    SELECT
        q.Id AS QuotationId,
        dr.DiagnosisAssignmentId,
        ROW_NUMBER() OVER (
            PARTITION BY q.Id
            ORDER BY qd.Id DESC, dr.Id DESC
        ) AS RowNum
    FROM dbo.Quotations q
    INNER JOIN dbo.QuotationDetails qd ON qd.QuotationId = q.Id
    INNER JOIN dbo.DiagnosisRecommendations dr ON dr.Id = qd.DiagnosisRecommendationId
    WHERE dr.DiagnosisAssignmentId IS NOT NULL
),
AssignmentBackfill AS (
    SELECT
        QuotationId,
        DiagnosisAssignmentId
    FROM AssignmentCandidates
    WHERE RowNum = 1
)
UPDATE q
SET q.DiagnosisAssignmentId = ab.DiagnosisAssignmentId
FROM dbo.Quotations q
INNER JOIN AssignmentBackfill ab ON ab.QuotationId = q.Id
WHERE q.DiagnosisAssignmentId IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE q
SET q.DiagnosisId = da.DiagnosisId
FROM dbo.Quotations q
INNER JOIN dbo.DiagnosisAssignments da ON da.Id = q.DiagnosisAssignmentId
WHERE q.DiagnosisId IS NULL
  AND q.DiagnosisAssignmentId IS NOT NULL;
");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_DiagnosisAssignmentId",
                table: "Quotations",
                column: "DiagnosisAssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_DiagnosisAssignments",
                table: "Quotations",
                column: "DiagnosisAssignmentId",
                principalTable: "DiagnosisAssignments",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_DiagnosisAssignments",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_DiagnosisAssignmentId",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "DiagnosisAssignmentId",
                table: "Quotations");
        }
    }
}

