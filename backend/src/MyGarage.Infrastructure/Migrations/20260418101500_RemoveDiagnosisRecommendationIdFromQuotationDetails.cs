using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260418101500_RemoveDiagnosisRecommendationIdFromQuotationDetails")]
    public partial class RemoveDiagnosisRecommendationIdFromQuotationDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE q
SET q.DiagnosisAssignmentId = da.Id
FROM dbo.Quotations q
CROSS APPLY (
    SELECT TOP (1) x.Id
    FROM dbo.DiagnosisAssignments x
    WHERE x.DiagnosisId = q.DiagnosisId
    ORDER BY x.CreatedAt DESC, x.Id DESC
) da
WHERE q.DiagnosisAssignmentId IS NULL
  AND q.DiagnosisId IS NOT NULL;");

            migrationBuilder.Sql(@"
UPDATE q
SET q.DiagnosisId = da.DiagnosisId
FROM dbo.Quotations q
INNER JOIN dbo.DiagnosisAssignments da ON da.Id = q.DiagnosisAssignmentId
WHERE q.DiagnosisId IS NULL
  AND q.DiagnosisAssignmentId IS NOT NULL;");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_QuotationDetails_DiagnosisRecommendations')
BEGIN
    ALTER TABLE dbo.QuotationDetails DROP CONSTRAINT FK_QuotationDetails_DiagnosisRecommendations;
END");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QuotationDetails_ServiceOrderId_DiagnosisRecommendationId' AND object_id = OBJECT_ID(N'dbo.QuotationDetails'))
BEGIN
    DROP INDEX IX_QuotationDetails_ServiceOrderId_DiagnosisRecommendationId ON dbo.QuotationDetails;
END");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QuotationDetails_DiagnosisRecommendationId' AND object_id = OBJECT_ID(N'dbo.QuotationDetails'))
BEGIN
    DROP INDEX IX_QuotationDetails_DiagnosisRecommendationId ON dbo.QuotationDetails;
END");

            migrationBuilder.DropColumn(
                name: "DiagnosisRecommendationId",
                table: "QuotationDetails");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DiagnosisRecommendationId",
                table: "QuotationDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_DiagnosisRecommendationId",
                table: "QuotationDetails",
                column: "DiagnosisRecommendationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_ServiceOrderId_DiagnosisRecommendationId",
                table: "QuotationDetails",
                columns: new[] { "ServiceOrderId", "DiagnosisRecommendationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDetails_DiagnosisRecommendations",
                table: "QuotationDetails",
                column: "DiagnosisRecommendationId",
                principalTable: "DiagnosisRecommendations",
                principalColumn: "Id");
        }
    }
}
