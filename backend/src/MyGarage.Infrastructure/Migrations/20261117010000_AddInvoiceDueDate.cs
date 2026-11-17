using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable
namespace MyGarage.Migrations;

[DbContext(typeof(GarageContext))]
[Migration("20261117010000_AddInvoiceDueDate")]
public sealed class AddInvoiceDueDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<DateTime>(name: "DueDate", table: "Invoices", type: "date", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "DueDate", table: "Invoices");
}
