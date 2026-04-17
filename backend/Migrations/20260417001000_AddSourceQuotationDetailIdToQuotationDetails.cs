using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260417001000_AddSourceQuotationDetailIdToQuotationDetails")]
    public partial class AddSourceQuotationDetailIdToQuotationDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourceQuotationDetailId",
                table: "QuotationDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_SourceQuotationDetailId",
                table: "QuotationDetails",
                column: "SourceQuotationDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDetails_SourceQuotationDetail",
                table: "QuotationDetails",
                column: "SourceQuotationDetailId",
                principalTable: "QuotationDetails",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuotationDetails_SourceQuotationDetail",
                table: "QuotationDetails");

            migrationBuilder.DropIndex(
                name: "IX_QuotationDetails_SourceQuotationDetailId",
                table: "QuotationDetails");

            migrationBuilder.DropColumn(
                name: "SourceQuotationDetailId",
                table: "QuotationDetails");
        }
    }
}
