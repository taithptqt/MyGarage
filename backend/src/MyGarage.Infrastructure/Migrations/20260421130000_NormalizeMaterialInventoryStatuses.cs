using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20260421130000_NormalizeMaterialInventoryStatuses")]
    public partial class NormalizeMaterialInventoryStatuses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
/* Normalize legacy statuses/conditions to match active check constraints. */

UPDATE dbo.MaterialRequestDetails
SET Status = CASE
    WHEN UPPER(ISNULL(Status, '')) IN ('ISSUED', 'COMPLETED', 'RETURNED') THEN 'COMPLETED'
    WHEN UPPER(ISNULL(Status, '')) IN ('PARTIALLY_ISSUED', 'PARTIAL') THEN 'PARTIAL'
    WHEN UPPER(ISNULL(Status, '')) IN ('CANCELED', 'CANCELLED') THEN 'CANCELED'
    ELSE 'PENDING'
END
WHERE UPPER(ISNULL(Status, '')) NOT IN ('PENDING', 'PARTIAL', 'COMPLETED', 'CANCELED')
   OR UPPER(ISNULL(Status, '')) IN ('ISSUED', 'PARTIALLY_ISSUED', 'RETURNED', 'CANCELLED');

UPDATE dbo.MaterialRequests
SET Status = CASE
    WHEN UPPER(ISNULL(Status, '')) IN ('ISSUED', 'COMPLETED') THEN 'COMPLETED'
    WHEN UPPER(ISNULL(Status, '')) IN ('PARTIALLY_ISSUED', 'PARTIAL') THEN 'PARTIAL'
    WHEN UPPER(ISNULL(Status, '')) IN ('CANCELED', 'CANCELLED') THEN 'CANCELED'
    ELSE 'PENDING'
END
WHERE UPPER(ISNULL(Status, '')) NOT IN ('PENDING', 'PARTIAL', 'COMPLETED', 'CANCELED')
   OR UPPER(ISNULL(Status, '')) IN ('ISSUED', 'PARTIALLY_ISSUED', 'CANCELLED');

UPDATE dbo.MaterialReturns
SET ItemCondition = CASE
    WHEN UPPER(ISNULL(ItemCondition, '')) IN ('GOOD', 'NEW', 'SEALED', 'NEW_SEALED') THEN 'NEW_SEALED'
    WHEN UPPER(ISNULL(ItemCondition, '')) IN ('OPENED', 'OPEN', 'OPENED_BULK') THEN 'OPENED_BULK'
    WHEN UPPER(ISNULL(ItemCondition, '')) IN ('DAMAGED', 'DAMAGED_BY_STAFF') THEN 'DAMAGED_BY_STAFF'
    WHEN UPPER(ISNULL(ItemCondition, '')) IN ('DEFECTIVE', 'DEFECT') THEN 'DEFECTIVE'
    ELSE 'NEW_SEALED'
END
WHERE UPPER(ISNULL(ItemCondition, '')) NOT IN ('NEW_SEALED', 'OPENED_BULK', 'DEFECTIVE', 'DAMAGED_BY_STAFF')
   OR UPPER(ISNULL(ItemCondition, '')) IN ('GOOD', 'NEW', 'SEALED', 'OPENED', 'OPEN', 'DAMAGED', 'DEFECT');

UPDATE dbo.MaterialReturns
SET Status = CASE
    WHEN UPPER(ISNULL(Status, '')) IN ('DONE', 'SUCCESS', 'COMPLETED') THEN 'COMPLETED'
    WHEN UPPER(ISNULL(Status, '')) IN ('REJECT', 'REJECTED') THEN 'REJECTED'
    ELSE 'PENDING'
END
WHERE UPPER(ISNULL(Status, '')) NOT IN ('PENDING', 'REJECTED', 'COMPLETED')
   OR UPPER(ISNULL(Status, '')) IN ('DONE', 'SUCCESS', 'REJECT');
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            /* Intentionally no-op: normalization is data-corrective and non-reversible. */
        }
    }
}
