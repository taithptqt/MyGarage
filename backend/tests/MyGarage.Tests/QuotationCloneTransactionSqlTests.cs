using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.Infrastructure.Persistence.Stores.Workshop;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class QuotationCloneTransactionSqlTests
{
    [LocalSqlFact]
    public async Task Clone_WhenCopyingDiscountFails_RollsBackNewQuotationAndSupersedingSource()
    {
        var name = "MyGarageQuotationClone_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"CREATE DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            long sourceId;
            long staffId;
            await using (var setup = new GarageContext(options))
            {
                // Match the existing SQL integration fixture: use the real EF model while
                // excluding legacy DDL that SQL Server cannot create in an empty database.
                var script = setup.Database.GenerateCreateScript();
                script = Regex.Replace(script, @"CREATE UNIQUE INDEX \[UQ_ServiceOrder_DiscountCategory\][\s\S]*?;", "", RegexOptions.IgnoreCase);
                script = script.Replace("ON DELETE CASCADE", "ON DELETE NO ACTION", StringComparison.OrdinalIgnoreCase);
                script = Regex.Replace(script, @"ALTER TABLE\s+\[[^\]]+\]\s+ADD CONSTRAINT\s+\[[^\]]+\]\s+FOREIGN KEY[\s\S]*?;", "", RegexOptions.IgnoreCase);
                foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                    if (!string.IsNullOrWhiteSpace(batch)) await setup.Database.ExecuteSqlRawAsync(batch);

                var branch = new Branch { Name = "Test branch", Status = "ACTIVE" };
                var customer = new Customer { FullName = "Test customer", Type = "INDIVIDUAL" };
                var staff = new Staff { FullName = "Test staff", Phone = "0900000001", PasswordHash = "hash", Branch = branch, Status = "ACTIVE" };
                var brand = new VehicleBrand { Code = "TEST", Name = "Test brand" };
                var model = new VehicleModel { Brand = brand, ModelName = "Test model" };
                var vehicle = new Vehicle { Model = model, LicensePlate = "TEST-001" };
                var order = new ServiceOrder { Code = "SO-CLONE", Customer = customer, Vehicle = vehicle, OriginBranch = branch, ExecuteBranch = branch, Status = "DIAGNOSING" };
                var diagnosis = new Diagnosis { ServiceOrder = order };
                var assignment = new DiagnosisAssignment { Diagnosis = diagnosis, EventType = "ASSIGNED" };
                var source = new Quotation { Code = "QT-CLONE", ServiceOrder = order, Diagnosis = diagnosis, DiagnosisAssignment = assignment, VersionNo = 1, Status = "DRAFT", QuotationType = "REPAIR", InsuranceChangeType = "NONE" };
                var line = new QuotationDetail { Quotation = source, ServiceOrder = order, ItemType = "OTHER", ItemName = "Work", Quantity = 1, UnitPriceAtTime = 100, SubTotal = 100, FinalAmount = 90, PayerType = "CUSTOMER", CustomerCoveredAmount = 90, Status = "ACTIVE", ApprovalStatus = "PENDING" };
                setup.AddRange(order, diagnosis, assignment, source, line, staff);
                setup.QuotationDiscounts.Add(new QuotationDiscount { Quotation = source, QuotationDetail = line, DiscountType = "MANUAL", ValueType = "AMOUNT", AppliedAmount = 10, DiscountValue = 10, AppliedByNavigation = staff });
                await setup.SaveChangesAsync();
                sourceId = source.Id;
                staffId = staff.Id;
            }

            var failingOptions = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString)
                .AddInterceptors(new RejectCopiedDiscountInterceptor()).Options;
            await using (var db = new GarageContext(failingOptions))
            {
                var store = new EfQuotationWorkflowStore(db, new ConfigurationBuilder().Build(), NullLogger<EfQuotationWorkflowStore>.Instance);
                var query = new QuotationQueryService(store, store, store);
                var service = new QuotationBuildService(store, store, store, query);
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloneQuotationAsync(sourceId, new QuotationCloneRequestDto { ActorStaffId = staffId }));
                Assert.Equal("Simulated copied discount failure", ex.Message);
            }

            await using var verify = new GarageContext(options);
            var quotations = await verify.Quotations.AsNoTracking().ToListAsync();
            Assert.Equal(sourceId, Assert.Single(quotations).Id);
            Assert.Equal("DRAFT", quotations[0].Status);
            Assert.Equal(1, await verify.QuotationDetails.CountAsync());
            Assert.Equal(1, await verify.QuotationDiscounts.CountAsync());
        }
        finally
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class RejectCopiedDiscountInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<QuotationDiscount>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Simulated copied discount failure");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
