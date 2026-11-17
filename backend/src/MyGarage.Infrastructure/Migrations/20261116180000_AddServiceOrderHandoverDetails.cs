using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations;

[DbContext(typeof(GarageContext))]
[Migration("20261116180000_AddServiceOrderHandoverDetails")]
public sealed class AddServiceOrderHandoverDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "HandoverReceiverName", table: "ServiceOrders", type: "nvarchar(255)", maxLength: 255, nullable: true);
        migrationBuilder.AddColumn<string>(name: "HandoverReceiverPhone", table: "ServiceOrders", type: "nvarchar(30)", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<string>(name: "HandoverNotes", table: "ServiceOrders", type: "nvarchar(2000)", maxLength: 2000, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "HandoverReceiverName", table: "ServiceOrders");
        migrationBuilder.DropColumn(name: "HandoverReceiverPhone", table: "ServiceOrders");
        migrationBuilder.DropColumn(name: "HandoverNotes", table: "ServiceOrders");
    }
}