using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Accounting;
using MyGarage.Data;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class AccountingDashboardPayablesSqlTests
{
    [LocalSqlFact]
    public async Task Dashboard_QueriesOpenPayablesOnlyWithinBranch()
    {
        var database = "MyGaragePayables_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = database };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{database}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using var db = new GarageContext(options);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE dbo.accounts_payable (id bigint NOT NULL PRIMARY KEY, branch_id bigint NULL, supplier_invoice_no nvarchar(50), due_date date NOT NULL, outstanding_amount decimal(18,2) NOT NULL, status varchar(30) NOT NULL);
                CREATE TABLE dbo.RevenueSnapshots (SnapshotDate date NOT NULL, BranchId bigint, TotalRevenue decimal(18,2), NetProfit decimal(18,2), NetCashFlow decimal(18,2));
                CREATE TABLE dbo.ServiceOrders (Id bigint PRIMARY KEY, ExecuteBranchId bigint, OriginBranchId bigint);
                CREATE TABLE dbo.RetailOrders (Id bigint PRIMARY KEY, BranchId bigint);
                CREATE TABLE dbo.Invoices (Id bigint PRIMARY KEY, ServiceOrderId bigint, RetailOrderId bigint, PayerType varchar(30), PaymentStatus varchar(30), TotalAmount decimal(18,2), PaidAmount decimal(18,2));
                CREATE TABLE dbo.Payments (Id bigint PRIMARY KEY, InvoiceId bigint, ServiceOrderId bigint, RetailOrderId bigint, Direction varchar(20), Type varchar(20), Status varchar(20), Amount decimal(18,2), CreatedAt datetime2);
                CREATE TABLE dbo.finance_approval_requests (id bigint PRIMARY KEY, request_type varchar(40), status varchar(20), created_at datetime2, updated_at datetime2);
                INSERT INTO dbo.finance_approval_requests VALUES
                  (1,'PAYMENT','PENDING','2026-05-01T10:00:00','2026-05-01T10:00:00'),
                  (2,'PAYMENT','APPROVED','2026-05-01T11:00:00','2026-05-01T11:00:00');
                INSERT INTO dbo.accounts_payable VALUES
                  (1,1,'A', '2000-01-01',200,'OPEN'),
                  (2,1,'B', '2099-01-01',300,'PARTIAL'),
                  (3,2,'C', '2000-01-01',900,'OPEN'),
                  (4,1,'D', '2000-01-01',800,'VOID');
                INSERT INTO dbo.RetailOrders (Id, BranchId) VALUES (10,1),(20,2);
                INSERT INTO dbo.Invoices (Id, RetailOrderId, PayerType, PaymentStatus, TotalAmount, PaidAmount) VALUES
                  (101,10,'CUSTOMER','UNPAID',500,0),
                  (102,10,'CUSTOMER','PARTIAL',300,100),
                  (103,20,'CUSTOMER','UNPAID',900,0),
                  (104,10,'INSURANCE','UNPAID',800,0),
                  (105,10,'CUSTOMER','PAID',700,700);
                INSERT INTO dbo.Payments (Id, RetailOrderId, Direction, Type, Status, Amount, CreatedAt) VALUES
                  (1,10,'INFLOW','PAYMENT','COMPLETED',100,'2026-05-01T15:00:00'),
                  (2,20,'INFLOW','PAYMENT','COMPLETED',900,'2026-05-01T16:00:00'),
                  (3,10,'INFLOW','PAYMENT','COMPLETED',300,'2026-05-02T12:00:00'),
                  (4,10,'OUTFLOW','REFUND','COMPLETED',20,'2026-05-01T10:00:00'),
                  (5,20,'OUTFLOW','REFUND','COMPLETED',30,'2026-05-01T11:00:00');
                """);
            var service = new AccountingFinanceWorkflowService(new AccountingCrudService(db));
            var result = await service.GetDashboardV2Async(null, null, 1, 1);
            var payload = JsonSerializer.SerializeToElement(result.Payload);
            Assert.True(payload.GetProperty("payablesAvailable").GetBoolean());
            var summary = payload.GetProperty("summary");
            Assert.Equal(500m, summary.GetProperty("totalOutstandingAp").GetDecimal());
            Assert.Equal(200m, summary.GetProperty("overdueApAmount").GetDecimal());
            Assert.True(payload.GetProperty("receivablesAvailable").GetBoolean());
            Assert.Equal(700m, summary.GetProperty("totalOutstandingAr").GetDecimal());
            Assert.Equal(JsonValueKind.Null, summary.GetProperty("overdueArCount").ValueKind);
            Assert.Equal(1, payload.GetProperty("invoiceWatchlist").GetArrayLength());
            var row = Assert.Single(payload.GetProperty("apWatchlist").EnumerateArray());
            Assert.Equal(1L, row.GetProperty("id").GetInt64());

            var sameDay = await service.GetDashboardV2Async(new DateTime(2026, 5, 1), new DateTime(2026, 5, 1), 20, 1);
            var sameDayPayload = JsonSerializer.SerializeToElement(sameDay.Payload);
            Assert.True(sameDayPayload.GetProperty("paymentHistoryAvailable").GetBoolean());
            var payments = sameDayPayload.GetProperty("paymentHistory").EnumerateArray().ToArray();
            Assert.Equal(new long[] { 1, 4 }, payments.Select(p => p.GetProperty("id").GetInt64()));
            Assert.True(sameDayPayload.GetProperty("refundTrailAvailable").GetBoolean());
            var refund = Assert.Single(sameDayPayload.GetProperty("refundTrail").EnumerateArray());
            Assert.Equal(4L, refund.GetProperty("id").GetInt64());
            Assert.False(sameDayPayload.GetProperty("approvalTrailAvailable").GetBoolean());
            Assert.Equal(JsonValueKind.Null, sameDayPayload.GetProperty("summary").GetProperty("pendingApprovalCount").ValueKind);

            var global = await service.GetDashboardV2Async(new DateTime(2026, 5, 1), new DateTime(2026, 5, 1), 20, null);
            var globalPayload = JsonSerializer.SerializeToElement(global.Payload);
            Assert.True(globalPayload.GetProperty("approvalTrailAvailable").GetBoolean());
            Assert.Equal(1, globalPayload.GetProperty("summary").GetProperty("pendingApprovalCount").GetInt32());
            Assert.Equal(2, globalPayload.GetProperty("approvalTrail").GetArrayLength());
            Assert.Equal(1600m, globalPayload.GetProperty("summary").GetProperty("totalOutstandingAr").GetDecimal());
            Assert.Equal(3, globalPayload.GetProperty("invoiceWatchlist").GetArrayLength());

            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.Invoices (Id, RetailOrderId, PayerType, PaymentStatus, TotalAmount, PaidAmount) VALUES (106,20,'CUSTOMER','UNPAID',100,NULL)");
            var incomplete = JsonSerializer.SerializeToElement((await service.GetDashboardV2Async(null, null, 20, 2)).Payload);
            Assert.False(incomplete.GetProperty("receivablesAvailable").GetBoolean());
            Assert.Equal(JsonValueKind.Null, incomplete.GetProperty("summary").GetProperty("totalOutstandingAr").ValueKind);

            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Invoices ADD DueDate date NULL;");
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE dbo.Invoices SET DueDate = DATEADD(day, -10, CAST(GETDATE() AS date)) WHERE Id = 101;
                UPDATE dbo.Invoices SET DueDate = DATEADD(day, -40, CAST(GETDATE() AS date)) WHERE Id = 102;
                """);
            var aged = JsonSerializer.SerializeToElement((await service.GetDashboardV2Async(null, null, 20, 1)).Payload);
            Assert.True(aged.GetProperty("detailsAvailable").GetBoolean());
            Assert.Equal(2, aged.GetProperty("summary").GetProperty("overdueArCount").GetInt32());
            var buckets = aged.GetProperty("agingBuckets").EnumerateArray().ToArray();
            Assert.Contains(buckets, b => b.GetProperty("label").GetString() == "1–30 ngày" && b.GetProperty("amount").GetDecimal() == 500m);
            Assert.Contains(buckets, b => b.GetProperty("label").GetString() == "31–60 ngày" && b.GetProperty("amount").GetDecimal() == 200m);

            await db.Database.ExecuteSqlRawAsync("UPDATE dbo.Invoices SET DueDate = NULL WHERE Id = 102");
            var missingDueDate = JsonSerializer.SerializeToElement((await service.GetDashboardV2Async(null, null, 20, 1)).Payload);
            Assert.False(missingDueDate.GetProperty("detailsAvailable").GetBoolean());
            Assert.Equal(JsonValueKind.Null, missingDueDate.GetProperty("summary").GetProperty("overdueArCount").ValueKind);
            Assert.Contains(missingDueDate.GetProperty("agingBuckets").EnumerateArray(), b => b.GetProperty("label").GetString() == "Chưa có hạn");
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
