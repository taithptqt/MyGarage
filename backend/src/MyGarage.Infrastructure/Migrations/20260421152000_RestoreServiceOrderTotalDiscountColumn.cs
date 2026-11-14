using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260421152000_RestoreServiceOrderTotalDiscountColumn")]
    public partial class RestoreServiceOrderTotalDiscountColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
IF COL_LENGTH(N'dbo.ServiceOrders', N'TotalDiscount') IS NULL
BEGIN
    ALTER TABLE dbo.ServiceOrders
    ADD TotalDiscount DECIMAL(18,2) NULL;
    EXEC(N'UPDATE dbo.ServiceOrders
          SET TotalDiscount = 0
          WHERE TotalDiscount IS NULL;');
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
IF COL_LENGTH(N'dbo.ServiceOrders', N'TotalDiscount') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ServiceOrders
    DROP COLUMN TotalDiscount;
END
""");
        }
    }
}
