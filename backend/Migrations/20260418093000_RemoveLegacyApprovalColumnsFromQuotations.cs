using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260418093000_RemoveLegacyApprovalColumnsFromQuotations")]
    public partial class RemoveLegacyApprovalColumnsFromQuotations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TechnicalApprovedBy",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "TechnicalApprovedAt",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "TeamLeaderApprovalStatus",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "FinalApprovedBy",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "FinalApprovedAt",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "FinalApprovalStatus",
                table: "Quotations");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "TeamLeaderApprovalStatus",
                table: "Quotations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FinalApprovedBy",
                table: "Quotations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalApprovedAt",
                table: "Quotations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalApprovalStatus",
                table: "Quotations",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
