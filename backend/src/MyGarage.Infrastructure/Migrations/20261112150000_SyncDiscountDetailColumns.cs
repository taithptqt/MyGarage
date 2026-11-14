using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;

#nullable disable

namespace MyGarage.Migrations
{
    [DbContext(typeof(GarageContext))]
    [Migration("20261112150000_SyncDiscountDetailColumns")]
    public partial class SyncDiscountDetailColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
IF COL_LENGTH(N'dbo.ServiceOrderDiscounts', N'ServiceOrderDetailId') IS NULL
BEGIN
    ALTER TABLE dbo.ServiceOrderDiscounts
    ADD ServiceOrderDetailId BIGINT NULL;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ServiceOrderDiscounts_ServiceOrderDetailId'
      AND object_id = OBJECT_ID(N'dbo.ServiceOrderDiscounts')
)
BEGIN
    CREATE INDEX IX_ServiceOrderDiscounts_ServiceOrderDetailId
        ON dbo.ServiceOrderDiscounts(ServiceOrderDetailId);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_ServiceOrderDiscounts_ServiceOrderDetails_ServiceOrderDetailId'
)
BEGIN
    ALTER TABLE dbo.ServiceOrderDiscounts
    ADD CONSTRAINT FK_ServiceOrderDiscounts_ServiceOrderDetails_ServiceOrderDetailId
        FOREIGN KEY (ServiceOrderDetailId) REFERENCES dbo.ServiceOrderDetails(Id);
END

IF COL_LENGTH(N'dbo.QuotationDiscounts', N'QuotationDetailId') IS NULL
BEGIN
    ALTER TABLE dbo.QuotationDiscounts
    ADD QuotationDetailId BIGINT NULL;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_QuotationDiscounts_QuotationDetailId'
      AND object_id = OBJECT_ID(N'dbo.QuotationDiscounts')
)
BEGIN
    CREATE INDEX IX_QuotationDiscounts_QuotationDetailId
        ON dbo.QuotationDiscounts(QuotationDetailId);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_QuotationDiscounts_QuotationDetails_QuotationDetailId'
)
BEGIN
    ALTER TABLE dbo.QuotationDiscounts
    ADD CONSTRAINT FK_QuotationDiscounts_QuotationDetails_QuotationDetailId
        FOREIGN KEY (QuotationDetailId) REFERENCES dbo.QuotationDetails(Id);
END

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.SalaryCoefficients')
      AND name = N'CoefficientValue'
      AND (precision <> 18 OR scale <> 4)
)
BEGIN
    ALTER TABLE dbo.SalaryCoefficients
    ALTER COLUMN CoefficientValue DECIMAL(18, 4) NOT NULL;
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_ServiceOrderDiscounts_ServiceOrderDetails_ServiceOrderDetailId'
)
BEGIN
    ALTER TABLE dbo.ServiceOrderDiscounts
    DROP CONSTRAINT FK_ServiceOrderDiscounts_ServiceOrderDetails_ServiceOrderDetailId;
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ServiceOrderDiscounts_ServiceOrderDetailId'
      AND object_id = OBJECT_ID(N'dbo.ServiceOrderDiscounts')
)
BEGIN
    DROP INDEX IX_ServiceOrderDiscounts_ServiceOrderDetailId ON dbo.ServiceOrderDiscounts;
END

IF COL_LENGTH(N'dbo.ServiceOrderDiscounts', N'ServiceOrderDetailId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ServiceOrderDiscounts
    DROP COLUMN ServiceOrderDetailId;
END
""");
        }
    }
}
