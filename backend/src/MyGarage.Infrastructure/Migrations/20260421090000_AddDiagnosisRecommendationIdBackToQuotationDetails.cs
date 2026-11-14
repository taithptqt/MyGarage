using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260421090000_AddDiagnosisRecommendationIdBackToQuotationDetails")]
    public partial class AddDiagnosisRecommendationIdBackToQuotationDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDetails_DiagnosisRecommendations",
                table: "QuotationDetails",
                column: "DiagnosisRecommendationId",
                principalTable: "DiagnosisRecommendations",
                principalColumn: "Id");

            // Cleanup legacy column on ServiceOrderDetails if an older schema still has it.
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.ServiceOrderDetails', N'DiagnosisRecommendationId') IS NOT NULL
BEGIN
    DECLARE @dropFkSql NVARCHAR(MAX) = N'';

    SELECT @dropFkSql = @dropFkSql + N'ALTER TABLE dbo.ServiceOrderDetails DROP CONSTRAINT [' + fk.name + N'];'
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
    WHERE fkc.parent_object_id = OBJECT_ID(N'dbo.ServiceOrderDetails')
      AND c.name = N'DiagnosisRecommendationId';

    IF LEN(@dropFkSql) > 0
        EXEC sp_executesql @dropFkSql;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_ServiceOrderDetails_DiagnosisRecommendationId'
          AND object_id = OBJECT_ID(N'dbo.ServiceOrderDetails')
    )
        DROP INDEX IX_ServiceOrderDetails_DiagnosisRecommendationId ON dbo.ServiceOrderDetails;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_ServiceOrderDetails_DiagnoseRecommendationId'
          AND object_id = OBJECT_ID(N'dbo.ServiceOrderDetails')
    )
        DROP INDEX IX_ServiceOrderDetails_DiagnoseRecommendationId ON dbo.ServiceOrderDetails;

    ALTER TABLE dbo.ServiceOrderDetails DROP COLUMN DiagnosisRecommendationId;
END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuotationDetails_DiagnosisRecommendations",
                table: "QuotationDetails");

            migrationBuilder.DropIndex(
                name: "IX_QuotationDetails_DiagnosisRecommendationId",
                table: "QuotationDetails");

            migrationBuilder.DropColumn(
                name: "DiagnosisRecommendationId",
                table: "QuotationDetails");

            // Best-effort rollback for legacy schema compatibility.
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.ServiceOrderDetails', N'DiagnosisRecommendationId') IS NULL
BEGIN
    ALTER TABLE dbo.ServiceOrderDetails ADD DiagnosisRecommendationId bigint NULL;
END;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ServiceOrderDetails')
      AND name = N'DiagnosisRecommendationId'
)
AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ServiceOrderDetails_DiagnosisRecommendationId'
      AND object_id = OBJECT_ID(N'dbo.ServiceOrderDetails')
)
BEGIN
    CREATE INDEX IX_ServiceOrderDetails_DiagnosisRecommendationId
        ON dbo.ServiceOrderDetails (DiagnosisRecommendationId);
END");
        }
    }
}
