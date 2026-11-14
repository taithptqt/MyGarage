using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.Models;
using MyGarage.Repositories;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class VoucherConcurrentSqlTests
{
    [LocalSqlFact]
    public async Task Redemption_ConcurrentQuotaAndIdempotency_AreSerializedOnSqlServer()
    {
        var name = "MyGarageVoucherTest_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true };
        var test = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name, Pooling = false };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(test.ConnectionString).Options;
            await using var seed = new GarageContext(options, new HttpContextAccessor());
            var script = seed.Database.GenerateCreateScript();
            script = Regex.Replace(script, @"CREATE UNIQUE INDEX \[UQ_ServiceOrder_DiscountCategory\][\s\S]*?;", "", RegexOptions.IgnoreCase);
            script = script.Replace("ON DELETE CASCADE", "ON DELETE NO ACTION", StringComparison.OrdinalIgnoreCase);
            script = Regex.Replace(script, @"ALTER TABLE\s+\[[^\]]+\]\s+ADD CONSTRAINT\s+\[[^\]]+\]\s+FOREIGN KEY[\s\S]*?;", "", RegexOptions.IgnoreCase);
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                if (!string.IsNullOrWhiteSpace(batch)) await seed.Database.ExecuteSqlRawAsync(batch);
            // Simulate a database where SQL equality ignores both case and accents.
            await seed.Database.ExecuteSqlRawAsync("ALTER TABLE [VoucherRedemptions] ALTER COLUMN [IdempotencyKey] varchar(100) COLLATE Latin1_General_100_CI_AI NULL");
            var quota = new Voucher { Code = "SQL-QUOTA", VoucherName = "Quota", Type = "FIXED", Value = 10m,
                Status = "ACTIVE", IsActive = true, Timezone = "Asia/Ho_Chi_Minh", UsageLimit = 1,
                StartDate = new DateTime(2020, 1, 1), EndDate = new DateTime(2100, 1, 1) };
            var replay = new Voucher { Code = "SQL-REPLAY", VoucherName = "Replay", Type = "FIXED", Value = 10m,
                Status = "ACTIVE", IsActive = true, Timezone = "Asia/Ho_Chi_Minh", UsageLimit = 10,
                StartDate = new DateTime(2020, 1, 1), EndDate = new DateTime(2100, 1, 1) };
            var collationA = new Voucher { Code = "SQL-COLL-A", VoucherName = "A", Type = "FIXED", Value = 10m,
                Status = "ACTIVE", IsActive = true, Timezone = "Asia/Ho_Chi_Minh", UsageLimit = 10,
                StartDate = new DateTime(2020, 1, 1), EndDate = new DateTime(2100, 1, 1) };
            var collationB = new Voucher { Code = "SQL-COLL-B", VoucherName = "B", Type = "FIXED", Value = 10m,
                Status = "ACTIVE", IsActive = true, Timezone = "Asia/Ho_Chi_Minh", UsageLimit = 10,
                StartDate = new DateTime(2020, 1, 1), EndDate = new DateTime(2100, 1, 1) };
            var customer = new Customer { FullName = "SQL concurrency customer", Type = "INDIVIDUAL" };
            var brand = new VehicleBrand { Name = "SQL brand", Code = "SQL-C" };
            var model = new VehicleModel { Brand = brand, ModelName = "SQL model" };
            var vehicle = new Vehicle { Model = model, LicensePlate = "SQL-TEST" };
            seed.AddRange(quota, replay, collationA, collationB, customer, vehicle);
            await seed.SaveChangesAsync();
            async Task<MyGarage.DTOs.Responses.VoucherRedeemResultResponseDto> Redeem(long id, string key)
            {
                await using var db = new GarageContext(options, new HttpContextAccessor());
                var service = new VoucherRedemptionService(new VoucherRedemptionRepository(db), db);
                return await service.RedeemAsync(new VoucherRedemptionRequestDto { VoucherId = id, CustomerId = customer.Id, VehicleId = vehicle.Id,
                    EligibleAmount = 100m, IdempotencyKey = key });
            }
            var quotaResults = await Task.WhenAll(Redeem(quota.Id, "quota-a"), Redeem(quota.Id, "quota-b"));
            Assert.Single(quotaResults, x => x.Success);
            var replayResults = await Task.WhenAll(Redeem(replay.Id, "same-request"), Redeem(replay.Id, "same-request"));
            Assert.All(replayResults, x => Assert.True(x.Success));
            var collationResults = await Task.WhenAll(Redeem(collationA.Id, "rÉsumÉ"), Redeem(collationB.Id, "resume"));
            Assert.Single(collationResults, x => x.Success);
            Assert.Contains(collationResults, x => x.ReasonCode == "VCH_IDEMPOTENCY_CONFLICT");
            seed.ChangeTracker.Clear();
            Assert.Equal(1, await seed.VoucherRedemptions.CountAsync(x => x.VoucherId == quota.Id));
            Assert.Equal(1, await seed.VoucherRedemptions.CountAsync(x => x.VoucherId == replay.Id));
            Assert.Equal(1, await seed.VoucherRedemptions.CountAsync(x => x.IdempotencyKey == "resume"));
            Assert.Equal(1, (await seed.Vouchers.FindAsync(quota.Id))!.RedeemedCount);
            Assert.Equal(1, (await seed.Vouchers.FindAsync(replay.Id))!.RedeemedCount);
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
