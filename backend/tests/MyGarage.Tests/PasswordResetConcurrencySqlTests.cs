using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using Xunit;

namespace MyGarage.Tests;

public sealed class PasswordResetConcurrencySqlTests
{
    [LocalSqlFact]
    public async Task StaleTokenCannotBeConsumedAfterUseOrRevocation()
    {
        var name = "MyGarageResetConcurrency_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using var setup = new GarageContext(options);
            await setup.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.PasswordResetTokens (Id bigint IDENTITY PRIMARY KEY, CustomerId bigint NULL, StaffId bigint NULL, TokenHash varchar(64) NOT NULL, ExpiresAt datetime2 NOT NULL, UsedAt datetime2 NULL, RevokedAt datetime2 NULL, CreatedAt datetime2 NOT NULL)");
            await setup.Database.ExecuteSqlRawAsync("INSERT dbo.PasswordResetTokens (CustomerId, TokenHash, ExpiresAt, CreatedAt) VALUES (1, 'used', DATEADD(hour, 1, GETUTCDATE()), GETUTCDATE()), (1, 'revoked', DATEADD(hour, 1, GETUTCDATE()), GETUTCDATE())");
            foreach (var hash in new[] { "used", "revoked" })
            {
                await using var first = new GarageContext(options);
                await using var second = new GarageContext(options);
                var a = await first.PasswordResetTokens.SingleAsync(t => t.TokenHash == hash);
                var b = await second.PasswordResetTokens.SingleAsync(t => t.TokenHash == hash);
                if (hash == "used") a.UsedAt = DateTime.UtcNow; else a.RevokedAt = DateTime.UtcNow;
                await first.SaveChangesAsync();
                b.UsedAt = DateTime.UtcNow;
                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
            }
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
