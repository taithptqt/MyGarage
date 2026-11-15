using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataScopeSqlTests
{
    [LocalSqlFact]
    public async Task ServiceOrders_BranchAndCustomerScope_AreAppliedBySqlBeforePaging()
    {
        var name = "MyGarageODataScope_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using var db = new GarageContext(options);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.ServiceOrders (Id bigint NOT NULL PRIMARY KEY, ExecuteBranchId bigint NOT NULL, CustomerId bigint NOT NULL)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.ServiceOrders (Id, ExecuteBranchId, CustomerId) VALUES (1, 1, 100), (2, 2, 100), (3, 1, 101), (4, 1, 100)");
            var service = new ODataQueryService(db);
            ClaimsPrincipal Actor(long id, string role, long? branch = null)
            {
                var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()), new(ClaimTypes.Role, role) };
                if (branch.HasValue) claims.Add(new Claim("branch_id", branch.Value.ToString()));
                return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            }
            var branch = service.ServiceOrders(Actor(200, "Staff", 1));
            var customer = service.ServiceOrders(Actor(100, "Customer"));
            Assert.Contains("WHERE", branch.Select(x => x.Id).ToQueryString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(3, await branch.CountAsync());
            Assert.Equal(new long[] { 3, 4 }, await branch.OrderBy(x => x.Id).Skip(1).Take(2).Select(x => x.Id).ToArrayAsync());
            Assert.Equal(new long[] { 1, 2, 4 }, await customer.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
            Assert.Empty(service.ServiceOrders(Actor(200, "Staff")));
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
