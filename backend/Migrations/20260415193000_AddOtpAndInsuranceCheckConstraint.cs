using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyGarage.Migrations
{
    public partial class AddOtpAndInsuranceCheckConstraint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OtpCodeHash",
                table: "QuotationAccessLinks",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OtpExpiresAt",
                table: "QuotationAccessLinks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OtpAttemptCount",
                table: "QuotationAccessLinks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OtpVerifiedAt",
                table: "QuotationAccessLinks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OtpLastSentAt",
                table: "QuotationAccessLinks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Quotations_InsuranceApprovalEvidence_Required",
                table: "Quotations",
                sql: "[Status] <> 'INSURANCE_APPROVED' OR NULLIF(LTRIM(RTRIM([InsuranceApprovalEvidenceUrl])), '') IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Quotations_InsuranceApprovalEvidence_Required",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "OtpCodeHash",
                table: "QuotationAccessLinks");

            migrationBuilder.DropColumn(
                name: "OtpExpiresAt",
                table: "QuotationAccessLinks");

            migrationBuilder.DropColumn(
                name: "OtpAttemptCount",
                table: "QuotationAccessLinks");

            migrationBuilder.DropColumn(
                name: "OtpVerifiedAt",
                table: "QuotationAccessLinks");

            migrationBuilder.DropColumn(
                name: "OtpLastSentAt",
                table: "QuotationAccessLinks");
        }
    }
}
