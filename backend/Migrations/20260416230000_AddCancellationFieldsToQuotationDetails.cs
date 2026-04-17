using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260416230000_AddCancellationFieldsToQuotationDetails")]
    public partial class AddCancellationFieldsToQuotationDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "QuotationDetails",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "ACTIVE");

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "QuotationDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CanceledBy",
                table: "QuotationDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE QuotationDetails
SET Status = 'ACTIVE'
WHERE Status IS NULL OR LTRIM(RTRIM(Status)) = '';
");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_CanceledBy",
                table: "QuotationDetails",
                column: "CanceledBy");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDetails_CanceledBy",
                table: "QuotationDetails",
                column: "CanceledBy",
                principalTable: "Staffs",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuotationDetails_CanceledBy",
                table: "QuotationDetails");

            migrationBuilder.DropIndex(
                name: "IX_QuotationDetails_CanceledBy",
                table: "QuotationDetails");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "QuotationDetails");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "QuotationDetails");

            migrationBuilder.DropColumn(
                name: "CanceledBy",
                table: "QuotationDetails");
        }
    }
}
