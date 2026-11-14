using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyGarage.Migrations
{
    /// <inheritdoc />
    public partial class ChatbotSecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SEC-06: Index hỗ trợ pre-filter voucher còn budget trong query ChatbotVoucherResolverService
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Vouchers_Chatbot_BudgetFilter'
                    AND object_id = OBJECT_ID(N'Vouchers')
                )
                BEGIN
                    CREATE NONCLUSTERED INDEX IX_Vouchers_Chatbot_BudgetFilter
                    ON [dbo].[Vouchers] ([IsDeleted], [IsActive], [Status], [StartDate], [EndDate])
                    INCLUDE ([BudgetLimit], [BudgetUsed])
                    WHERE [IsDeleted] IS NULL OR [IsDeleted] = 0;
                END
            ");

            // SEC-04: Index hỗ trợ idempotency guard kiểm tra duplicate DRAFT service order
            // (Customer + Vehicle + Status + OrderCategory)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_ServiceOrders_Chatbot_DraftGuard'
                    AND object_id = OBJECT_ID(N'ServiceOrders')
                )
                BEGIN
                    CREATE NONCLUSTERED INDEX IX_ServiceOrders_Chatbot_DraftGuard
                    ON [dbo].[ServiceOrders] ([CustomerId], [VehicleId], [Status], [OrderCategory])
                    WHERE [IsDeleted] IS NULL OR [IsDeleted] = 0;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS [IX_Vouchers_Chatbot_BudgetFilter] ON [dbo].[Vouchers];
                DROP INDEX IF EXISTS [IX_ServiceOrders_Chatbot_DraftGuard] ON [dbo].[ServiceOrders];
            ");
        }
    }
}
