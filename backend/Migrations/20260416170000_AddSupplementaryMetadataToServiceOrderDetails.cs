using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260416170000_AddSupplementaryMetadataToServiceOrderDetails")]
    public partial class AddSupplementaryMetadataToServiceOrderDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSupplementary",
                table: "ServiceOrderDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ServiceSource",
                table: "ServiceOrderDetails",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "INITIAL");

            migrationBuilder.AddColumn<string>(
                name: "SupplementaryReasonCode",
                table: "ServiceOrderDetails",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceQuotationId",
                table: "ServiceOrderDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SupplementaryRequestedByStaffId",
                table: "ServiceOrderDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupplementaryRequestedAt",
                table: "ServiceOrderDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomerApprovedAt",
                table: "ServiceOrderDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_SourceQuotationId",
                table: "ServiceOrderDetails",
                column: "SourceQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_SupplementaryRequestedByStaffId",
                table: "ServiceOrderDetails",
                column: "SupplementaryRequestedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrderDetails_SourceQuotation",
                table: "ServiceOrderDetails",
                column: "SourceQuotationId",
                principalTable: "Quotations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrderDetails_SupplementaryRequestedByStaff",
                table: "ServiceOrderDetails",
                column: "SupplementaryRequestedByStaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.Sql(@"
UPDATE sod
SET
    sod.SourceQuotationId = COALESCE(sod.SourceQuotationId, qd.QuotationId)
FROM dbo.ServiceOrderDetails sod
LEFT JOIN dbo.QuotationDetails qd ON qd.Id = sod.QuotationDetailId;

UPDATE sod
SET
    sod.ServiceSource =
        CASE
            WHEN q.Id IS NOT NULL AND q.VersionNo > 1 AND qd.DiagnosisRecommendationId IS NOT NULL THEN 'SUPPLEMENTARY_TECHNICIAN'
            WHEN q.Id IS NOT NULL AND q.VersionNo > 1 THEN 'SUPPLEMENTARY_CUSTOMER'
            ELSE 'INITIAL'
        END,
    sod.IsSupplementary =
        CASE
            WHEN q.Id IS NOT NULL AND q.VersionNo > 1 THEN 1
            ELSE 0
        END,
    sod.SupplementaryReasonCode =
        CASE
            WHEN q.Id IS NOT NULL AND q.VersionNo > 1 AND qd.DiagnosisRecommendationId IS NOT NULL THEN 'TECH_FINDING'
            WHEN q.Id IS NOT NULL AND q.VersionNo > 1 THEN 'CUSTOMER_REQUEST'
            ELSE NULL
        END,
    sod.SupplementaryRequestedByStaffId =
        CASE
            WHEN q.Id IS NOT NULL AND q.VersionNo > 1 THEN COALESCE(sod.SupplementaryRequestedByStaffId, q.CreatedBy, sod.CreatedBy, sod.UpdatedBy)
            ELSE NULL
        END,
    sod.SupplementaryRequestedAt =
        CASE
            WHEN q.Id IS NOT NULL AND q.VersionNo > 1 THEN COALESCE(sod.SupplementaryRequestedAt, q.CreatedAt, sod.CreatedAt, sod.UpdatedAt, GETDATE())
            ELSE NULL
        END,
    sod.CustomerApprovedAt =
        CASE
            WHEN q.Id IS NOT NULL AND q.VersionNo > 1 THEN COALESCE(sod.CustomerApprovedAt, q.AppliedAt, q.CustomerDecisionAt, sod.UpdatedAt, sod.CreatedAt, GETDATE())
            ELSE NULL
        END
FROM dbo.ServiceOrderDetails sod
LEFT JOIN dbo.QuotationDetails qd ON qd.Id = sod.QuotationDetailId
LEFT JOIN dbo.Quotations q ON q.Id = qd.QuotationId;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ServiceOrderDetails_SupplementaryReasonCode')
BEGIN
    ALTER TABLE dbo.ServiceOrderDetails DROP CONSTRAINT CK_ServiceOrderDetails_SupplementaryReasonCode;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ServiceOrderDetails_SupplementaryConsistency')
BEGIN
    ALTER TABLE dbo.ServiceOrderDetails DROP CONSTRAINT CK_ServiceOrderDetails_SupplementaryConsistency;
END
");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceOrderDetails_SupplementaryReasonCode",
                table: "ServiceOrderDetails",
                sql: "[SupplementaryReasonCode] IS NULL OR [SupplementaryReasonCode] IN ('TECH_FINDING','CUSTOMER_REQUEST')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceOrderDetails_SupplementaryConsistency",
                table: "ServiceOrderDetails",
                sql: "([ServiceSource]='INITIAL' AND [IsSupplementary]=0 AND [SupplementaryReasonCode] IS NULL) OR " +
                     "([ServiceSource]='SUPPLEMENTARY_TECHNICIAN' AND [IsSupplementary]=1 AND [SupplementaryReasonCode]='TECH_FINDING') OR " +
                     "([ServiceSource]='SUPPLEMENTARY_CUSTOMER' AND [IsSupplementary]=1 AND [SupplementaryReasonCode]='CUSTOMER_REQUEST')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ServiceOrderDetails_SupplementaryConsistency')
BEGIN
    ALTER TABLE dbo.ServiceOrderDetails DROP CONSTRAINT CK_ServiceOrderDetails_SupplementaryConsistency;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ServiceOrderDetails_SupplementaryReasonCode')
BEGIN
    ALTER TABLE dbo.ServiceOrderDetails DROP CONSTRAINT CK_ServiceOrderDetails_SupplementaryReasonCode;
END
");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOrderDetails_SourceQuotation",
                table: "ServiceOrderDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOrderDetails_SupplementaryRequestedByStaff",
                table: "ServiceOrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOrderDetails_SourceQuotationId",
                table: "ServiceOrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOrderDetails_SupplementaryRequestedByStaffId",
                table: "ServiceOrderDetails");

            migrationBuilder.DropColumn(
                name: "IsSupplementary",
                table: "ServiceOrderDetails");

            migrationBuilder.DropColumn(
                name: "ServiceSource",
                table: "ServiceOrderDetails");

            migrationBuilder.DropColumn(
                name: "SupplementaryReasonCode",
                table: "ServiceOrderDetails");

            migrationBuilder.DropColumn(
                name: "SourceQuotationId",
                table: "ServiceOrderDetails");

            migrationBuilder.DropColumn(
                name: "SupplementaryRequestedByStaffId",
                table: "ServiceOrderDetails");

            migrationBuilder.DropColumn(
                name: "SupplementaryRequestedAt",
                table: "ServiceOrderDetails");

            migrationBuilder.DropColumn(
                name: "CustomerApprovedAt",
                table: "ServiceOrderDetails");
        }
    }
}
