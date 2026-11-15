using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;
using MyGarage.Repositories;
using MyGarage.Controllers;
using MyGarage.Interfaces;
using MyGarage.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using AutoMapper;
using System.Security.Claims;
using Xunit;

namespace MyGarage.Tests;

public sealed class LocalSqlFactAttribute : FactAttribute
{
    public LocalSqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MYGARAGE_TEST_LOCALDB") != "1")
            Skip = "Set MYGARAGE_TEST_LOCALDB=1 to run isolated LocalDB migration test.";
    }
}

public class SqlServerMigrationTests
{
    [LocalSqlFact]
    public async Task RetailCatalog_TranslatesStockOrderingAndAccentSearchOnSqlServer()
    {
        var name = "MyGarageCodexTest_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true };
        var test = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name, Pooling = false };
        await using var masterConnection = new SqlConnection(master.ConnectionString);
        await masterConnection.OpenAsync();
        await using (var create = masterConnection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(test.ConnectionString).Options;
            await using var db = new GarageContext(options);
            var createScript = db.Database.GenerateCreateScript();
            // Existing legacy filtered index targets a computed column and is not accepted by SQL Server.
            createScript = Regex.Replace(createScript, @"CREATE UNIQUE INDEX \[UQ_ServiceOrder_DiscountCategory\][\s\S]*?;", "", RegexOptions.IgnoreCase);
            createScript = createScript.Replace("ON DELETE CASCADE", "ON DELETE NO ACTION", StringComparison.OrdinalIgnoreCase);
            createScript = Regex.Replace(createScript, @"ALTER TABLE\s+\[[^\]]+\]\s+ADD CONSTRAINT\s+\[[^\]]+\]\s+FOREIGN KEY[\s\S]*?;", "", RegexOptions.IgnoreCase);
            foreach (var batch in Regex.Split(createScript, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
            }
            var branch = new Branch { Name = "Main", Status = "ACTIVE" };
            var category = new Category { CategoryName = "Phanh" };
            var product = new Product { Name = "Má phanh", Sku = "P-01", Unit = "unit", IsActive = true, Category = category };
            var warehouse = new Warehouse { Branch = branch, Name = "Retail", Code = "R", Type = "RETAIL" };
            db.AddRange(branch, category, product, warehouse);
            db.Inventories.Add(new Inventory { Product = product, Warehouse = warehouse, Quantity = 3 });
            db.ProductImages.Add(new ProductImage { Product = product, ImageUrl = "/a.png", Description = "Dầu thắng" });
            await db.SaveChangesAsync();
            var paymentMethod = new PaymentMethod { MethodCode = "TEST", MethodName = "Test" };
            db.Payments.AddRange(
                new Payment { PayerId = 100, PayerType = "CUSTOMER", Method = paymentMethod, Amount = 10, Type = "PAYMENT", Direction = "INFLOW" },
                new Payment { PayerId = 101, PayerType = "CUSTOMER", Method = paymentMethod, Amount = 20, Type = "PAYMENT", Direction = "INFLOW" });
            await db.SaveChangesAsync();
            db.ServiceCategories.AddRange(
                new ServiceCategory { Code = "S1", Name = "Bảo dưỡng" },
                new ServiceCategory { Code = "S2", Name = "Phanh" });
            await db.SaveChangesAsync();
            var categories = await new MyGarage.Services.ServiceCategoryPageReader(db).ReadAsync("bảo", 1, 1, default);
            Assert.Equal(1, categories.Total);
            Assert.Equal("S1", Assert.Single(categories.Items).Code);
            var isolatedReader = new MyGarage.Infrastructure.Payments.CustomerPaymentsReader(db);
            var payments = await isolatedReader.ReadAsync(100, 1, 1, default);
            Assert.Equal(1, payments.Total);
            Assert.Equal(10m, Assert.Single(payments.Items).Amount);
            Assert.Empty((await isolatedReader.ReadAsync(101, 2, 1, default)).Items);
            Assert.Empty((await isolatedReader.ReadAsync(999, 1, 10, default)).Items);
            var retailOrder = new RetailOrder { BranchId = branch.Id, OrderCode = "SQL-INVOICE" };
            db.RetailOrders.Add(retailOrder);
            db.Invoices.Add(new Invoice { RetailOrder = retailOrder, PayerType = "CUSTOMER", TotalAmount = 100m });
            await db.SaveChangesAsync();
            var invoiceQuery = new MyGarage.Services.InvoiceQueryReader(db).Query(
                new MyGarage.Application.Payments.InvoiceScope(MyGarage.Application.Payments.InvoiceScopeKind.Branch, branch.Id));
            var invoices = await invoiceQuery.Where(x => x.TotalAmount >= 50m).OrderBy(x => x.Id).Take(1).ToListAsync();
            Assert.Single(invoices);
            Assert.Equal(branch.Id, invoices[0].BranchId);
            await using var factory = new TestWebApplicationFactory();
            using var scope = factory.Services.CreateScope();
            var controller = new ProductsController(
                scope.ServiceProvider.GetRequiredService<IProductService>(),
                new MyGarage.Infrastructure.Queries.EfProductQueryService(db))
                { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
            var result = await controller.GetRetailCatalog("ma phanh", null, branch.Id, "name-asc", 1, 12, default);
            var ok = Assert.IsType<OkObjectResult>(result);
            using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(ok.Value));
            Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
            var byImage = Assert.IsType<OkObjectResult>(await controller.GetRetailCatalog("dau thang", category.Id, branch.Id, "price-asc", 1, 12, default));
            using var imageJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(byImage.Value));
            Assert.Equal(1, imageJson.RootElement.GetProperty("total").GetInt32());

            var brand = new VehicleBrand { Name = "Toyota", Code = "TOY" };
            var model = new VehicleModel { Brand = brand, ModelName = "Camry" };
            var vehicle = new Vehicle { Model = model, LicensePlate = "SQL-CAR" };
            var customer = new Customer { FullName = "Test Customer", Type = "INDIVIDUAL", Note = $"[PreferredBranchId:{branch.Id}]" };
            db.AddRange(brand, model, vehicle, customer);
            db.CustomerVehicles.Add(new CustomerVehicle { Customer = customer, Vehicle = vehicle, IsCurrentOwner = true });
            await db.SaveChangesAsync();
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.NameIdentifier, "200"), new Claim(ClaimTypes.Role, "Staff"),
                new Claim("role_code", "MANAGER"), new Claim("branch_id", branch.Id.ToString()) }, "test")) };
            var customerController = new CustomerAdministrationController(new MyGarage.Infrastructure.Queries.EfCustomerQueryService(db), scope.ServiceProvider.GetRequiredService<ICustomerService>())
                { ControllerContext = new ControllerContext { HttpContext = http } };
            var customerResult = Assert.IsType<OkObjectResult>(await customerController.GetPage(null, "camry", 1, 10, default));
            using var customerJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(customerResult.Value));
            Assert.Equal(1, customerJson.RootElement.GetProperty("total").GetInt32());

            var groups = Enumerable.Range(1, 3).Select(i => new GroupSession
            {
                Branch = branch, Customer = customer, GroupCode = $"SQL-GROUP-{i}", Status = "PENDING"
            }).ToArray();
            db.GroupSessions.AddRange(groups);
            for (var i = 0; i < groups.Length; i++)
            {
                for (var j = 0; j < 2; j++)
                    db.Appointments.Add(new Appointment { Branch = branch, Customer = customer, Vehicle = vehicle,
                        GroupSession = groups[i], Status = "PENDING", StartTime = new DateTime(2026, 11, 13, 8, 0, 0),
                        EndTime = new DateTime(2026, 11, 13, 9, 0, 0), CreatedAt = new DateTime(2026, 11, 10).AddMinutes(i * 2 + j) });
            }
            db.Appointments.Add(new Appointment { Branch = branch, Customer = customer, Vehicle = vehicle, Status = "CONFIRMED",
                StartTime = new DateTime(2026, 11, 13, 8, 0, 0), EndTime = new DateTime(2026, 11, 13, 9, 0, 0) });
            await db.SaveChangesAsync();
            var reception = new ReceptionAppointmentListController(new MyGarage.Infrastructure.Queries.EfReceptionAppointmentListQueryService(db), new MyGarage.Services.AppointmentDetailAssembler(new MyGarage.Infrastructure.Persistence.Stores.Appointments.EfAppointmentStore(db, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build())))
                { ControllerContext = new ControllerContext { HttpContext = http } };
            var receptionResult = Assert.IsType<OkObjectResult>(await reception.Get(null, "sql car", null,
                new DateTime(2026, 11, 13), new DateTime(2026, 11, 13), "NEWEST", "GROUP", 2, 1));
            using var receptionJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(receptionResult.Value));
            Assert.Equal(1, receptionJson.RootElement.GetProperty("singleCount").GetInt32());
            Assert.Equal(3, receptionJson.RootElement.GetProperty("groupCount").GetInt32());
            var receptionItems = receptionJson.RootElement.GetProperty("items");
            Assert.Equal(2, receptionItems.GetArrayLength());
            Assert.Equal(groups[1].Id, receptionItems[0].GetProperty("GroupSessionId").GetInt64());
            Assert.Equal(groups[1].Id, receptionItems[1].GetProperty("GroupSessionId").GetInt64());
            var filteredResult = Assert.IsType<OkObjectResult>(await reception.Get(null, "sql-group-3", "PENDING",
                new DateTime(2026, 11, 13), new DateTime(2026, 11, 13), "OLDEST", "GROUP", 1, 10));
            using var filteredJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(filteredResult.Value));
            Assert.Equal(1, filteredJson.RootElement.GetProperty("groupCount").GetInt32());
            Assert.Equal(groups[2].Id, filteredJson.RootElement.GetProperty("items")[0].GetProperty("GroupSessionId").GetInt64());

            var voucher = new Voucher { Code = "SQL-V", VoucherName = "Voucher SQL", Type = "FIXED", Status = "ACTIVE",
                Timezone = "Asia/Ho_Chi_Minh", IssuedCount = 2, RedeemedCount = 1, BudgetLimit = 100m, BudgetUsed = 25m };
            db.Vouchers.Add(voucher);
            await db.SaveChangesAsync();
            var redemption = new VoucherRedemption { VoucherId = voucher.Id, VoucherCode = voucher.Code,
                CustomerId = customer.Id, BranchId = branch.Id, ValidationStatus = "VALID", RedemptionStatus = "REDEEMED",
                DiscountAmount = 25m, AppliedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
            db.VoucherRedemptions.Add(redemption);
            await db.SaveChangesAsync();
            var voucherController = new VoucherAdministrationController(
                new MyGarage.Application.Vouchers.VoucherAdministrationService(
                    new MyGarage.Infrastructure.Vouchers.EfVoucherStore(db),
                    scope.ServiceProvider.GetRequiredService<IMapper>()));
            var report = Assert.IsType<OkObjectResult>(await voucherController.GetReportPage(1, 10, default));
            using var reportJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(report.Value));
            Assert.Equal(25m, reportJson.RootElement.GetProperty("budgetUsed").GetDecimal());
            var dashboard = Assert.IsType<OkObjectResult>(await voucherController.GetDashboard(default));
            using var dashboardJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(dashboard.Value));
            Assert.Equal(25m, dashboardJson.RootElement.GetProperty("totalDiscount").GetDecimal());
            var redemptionController = new VoucherRedemptionController(scope.ServiceProvider.GetRequiredService<IVoucherRedemptionService>(),
                new MyGarage.Infrastructure.Queries.EfVoucherRedemptionQueryService(db)) { ControllerContext = new ControllerContext { HttpContext = http } };
            var redemptionResult = Assert.IsType<OkObjectResult>(await redemptionController.GetAdminPage(
                $"SQL-V {redemption.Id} {customer.Id}", "discountDesc", 1, 10, default));
            using var redemptionJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(redemptionResult.Value));
            Assert.Equal(1, redemptionJson.RootElement.GetProperty("total").GetInt32());
            var campaign = new VoucherCampaign { Name = "SQL campaign", Status = "ACTIVE", BudgetLimit = 100m,
                BudgetUsed = 85m, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.VoucherCampaigns.Add(campaign);
            voucher.Campaign = campaign;
            db.VoucherCampaigns.Add(new VoucherCampaign { Name = "No budget", Status = "ACTIVE", BudgetLimit = 0m,
                StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var campaignController = new VoucherCampaignController(scope.ServiceProvider.GetRequiredService<IVoucherCampaignService>(),
                new MyGarage.Infrastructure.Queries.EfVoucherCampaignQueryService(db));
            var campaignResult = Assert.IsType<OkObjectResult>(await campaignController.GetAdminPage("SQL campaign", null, 1, 10, default));
            using var campaignJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(campaignResult.Value));
            Assert.Equal(1, campaignJson.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(1, campaignJson.RootElement.GetProperty("stats").GetProperty("budgetWarningCount").GetInt32());
        }
        finally
        {
            await using var drop = masterConnection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [LocalSqlFact]
    public async Task TokenMigrations_ApplyOnIsolatedSqlServerDatabase()
    {
        var name = "MyGarageCodexTest_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true };
        var test = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name, Pooling = false };
        await using var masterConnection = new SqlConnection(master.ConnectionString);
        await masterConnection.OpenAsync();
        await using (var create = masterConnection.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE [{name}]";
            await create.ExecuteNonQueryAsync();
        }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(test.ConnectionString).Options;
            await using var db = new GarageContext(options);
            var script = db.GetService<IMigrator>().GenerateScript("20261112150000_SyncDiscountDetailColumns");
            await using var connection = new SqlConnection(test.ConnectionString);
            await connection.OpenAsync();
            await using (var history = connection.CreateCommand())
            {
                history.CommandText = "CREATE TABLE [__EFMigrationsHistory] ([MigrationId] nvarchar(150) NOT NULL PRIMARY KEY, [ProductVersion] nvarchar(32) NOT NULL)";
                await history.ExecuteNonQueryAsync();
            }
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                await command.ExecuteNonQueryAsync();
            }
            await using var verify = connection.CreateCommand();
            verify.CommandText = "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.RevokedAuthTokens') AND name = 'IX_RevokedAuthTokens_ExpiresAtUtc'";
            Assert.Equal(1, (int)(await verify.ExecuteScalarAsync())!);

            await using (var setup = connection.CreateCommand())
            {
                setup.CommandText = """
                    CREATE TABLE ApprovalRequests (
                        Id bigint NOT NULL PRIMARY KEY, RequestType varchar(50) NOT NULL,
                        ServiceOrderDiscountId bigint NULL, GatePassId bigint NULL, MaterialReturnId bigint NULL,
                        RequestedBy bigint NOT NULL, ApproverId bigint NULL, Status varchar(20) NULL,
                        Reason nvarchar(max) NULL, RejectReason nvarchar(max) NULL,
                        CreatedAt datetime2 NULL, UpdatedAt datetime2 NULL
                    );
                    DECLARE @id int = 1;
                    WHILE @id <= 65
                    BEGIN
                        INSERT INTO ApprovalRequests (Id, RequestType, RequestedBy) VALUES (@id, 'TEST', 1);
                        SET @id = @id + 1;
                    END
                    """;
                await setup.ExecuteNonQueryAsync();
            }
            var (items, total) = await new GenericRepository<MyGarage.Models.ApprovalRequest>(db).GetPageByIdAsync(2, 50);
            Assert.Equal(65, total);
            Assert.Equal(15, items.Count);
            Assert.Equal(51, items[0].Id);
        }
        finally
        {
            // Only this test's GUID-named database is removed.
            await using var drop = masterConnection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
