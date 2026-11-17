using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

namespace MyGarage.Migrations;

[DbContext(typeof(GarageContext))]
[Migration("20261117020000_AddMaterialRequestReservedQuantity")]
public sealed class AddMaterialRequestReservedQuantity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<decimal>(name: "ReservedQuantity", table: "MaterialRequestDetails", type: "decimal(18,3)", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "ReservedQuantity", table: "MaterialRequestDetails");
}
