using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260418113000_AddQuotationConditionalApprovalMatrix")]
    public partial class AddQuotationConditionalApprovalMatrix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TechnicalApprovedBy",
                table: "Quotations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TechnicalApprovedAt",
                table: "Quotations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalApprovalStatus",
                table: "Quotations",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true,
                defaultValue: "PENDING");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresFinancialApproval",
                table: "Quotations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "FinancialApprovedBy",
                table: "Quotations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinancialApprovedAt",
                table: "Quotations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinancialApprovalStatus",
                table: "Quotations",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true,
                defaultValue: "NOT_REQUIRED");

            migrationBuilder.CreateTable(
                name: "BranchQuotationApprovalPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    MaxDiscountPercentWithoutManager = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 5m),
                    HighValueThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 50000000m),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchQuotationApprovalPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchQuotationApprovalPolicies_Branches",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationApprovalActions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<long>(type: "bigint", nullable: false),
                    ApprovalType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Decision = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ActorStaffId = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationApprovalActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationApprovalActions_Quotations",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_BranchQuotationApprovalPolicies_BranchId",
                table: "BranchQuotationApprovalPolicies",
                column: "BranchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationApprovalActions_QuotationId_ApprovalType_CreatedAt",
                table: "QuotationApprovalActions",
                columns: new[] { "QuotationId", "ApprovalType", "CreatedAt" });

            migrationBuilder.Sql(@"
UPDATE q
SET q.TechnicalApprovalStatus =
    CASE
        WHEN UPPER(LTRIM(RTRIM(ISNULL(q.[Status], '')))) = 'REJECTED' THEN 'REJECTED'
        WHEN UPPER(LTRIM(RTRIM(ISNULL(q.[Status], '')))) IN ('PENDING_INSURANCE','INSURANCE_APPROVED','PENDING_CUSTOMER','CUSTOMER_APPROVED','CUSTOMER_REJECTED','SENT_TO_CUSTOMER') THEN 'APPROVED'
        ELSE 'PENDING'
    END,
    q.TechnicalApprovedBy =
    CASE
        WHEN UPPER(LTRIM(RTRIM(ISNULL(q.[Status], '')))) IN ('PENDING_INSURANCE','INSURANCE_APPROVED','PENDING_CUSTOMER','CUSTOMER_APPROVED','CUSTOMER_REJECTED','SENT_TO_CUSTOMER')
            THEN COALESCE(q.ApprovedBy, q.UpdatedBy, q.CreatedBy)
        ELSE q.TechnicalApprovedBy
    END,
    q.TechnicalApprovedAt =
    CASE
        WHEN UPPER(LTRIM(RTRIM(ISNULL(q.[Status], '')))) IN ('PENDING_INSURANCE','INSURANCE_APPROVED','PENDING_CUSTOMER','CUSTOMER_APPROVED','CUSTOMER_REJECTED','SENT_TO_CUSTOMER')
            THEN COALESCE(q.ApprovedAt, q.UpdatedAt, q.CreatedAt)
        ELSE q.TechnicalApprovedAt
    END,
    q.RequiresFinancialApproval = 0,
    q.FinancialApprovalStatus = 'NOT_REQUIRED'
FROM dbo.Quotations q;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchQuotationApprovalPolicies");

            migrationBuilder.DropTable(
                name: "QuotationApprovalActions");

            migrationBuilder.DropColumn(
                name: "TechnicalApprovedBy",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "TechnicalApprovedAt",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "TechnicalApprovalStatus",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "RequiresFinancialApproval",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "FinancialApprovedBy",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "FinancialApprovedAt",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "FinancialApprovalStatus",
                table: "Quotations");
        }
    }
}
