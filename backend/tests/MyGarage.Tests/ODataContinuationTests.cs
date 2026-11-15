using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MyGarage.Application.Ai;
using MyGarage.Data;
using MyGarage.Infrastructure.Ai;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataContinuationTests
{
    [Theory]
    [InlineData("/odata")]
    [InlineData("/api/v1/odata")]
    public async Task ContinuationCrossesSkipLimitAndPreservesQuery(string prefix)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiKnowledgeCatalogReader>();
            services.AddScoped<IAiKnowledgeCatalogReader, ManyRowsReader>();
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        var uri = $"{prefix}/AiKnowledgeCatalog?$orderby=Id&$select=Id&$skip=10000&$count=true";
        using var first = await client.GetAsync(uri);
        Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Assert.Equal(10205, firstJson.RootElement.GetProperty("@odata.count").GetInt32());
        var firstRows = firstJson.RootElement.GetProperty("value").EnumerateArray().Select(x => x.GetProperty("Id").GetInt64()).ToArray();
        Assert.Equal(Enumerable.Range(10001, 100).Select(i => (long)i), firstRows);
        var next = firstJson.RootElement.GetProperty("@odata.nextLink").GetString();
        Assert.NotNull(next);
        using var second = await client.GetAsync(next);
        Assert.True(second.IsSuccessStatusCode, await second.Content.ReadAsStringAsync());
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        var secondRows = secondJson.RootElement.GetProperty("value").EnumerateArray().Select(x => x.GetProperty("Id").GetInt64()).ToArray();
        Assert.Equal(Enumerable.Range(10101, 100).Select(i => (long)i), secondRows);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{prefix}/AiKnowledgeCatalog?$skip=10001")).StatusCode);
    }

    private sealed class ManyRowsReader : IAiKnowledgeCatalogReader
    {
        public IQueryable<AiKnowledgeCatalogRow> Query() => Enumerable.Range(1, 10205)
            .Select(i => new AiKnowledgeCatalogRow { Id = i, Topic = "Entry" }).AsQueryable();
    }

    [LocalSqlFact]
    public async Task Continuation_ExecutesAgainstSql_WithDuplicateSortValuesAndFilter()
    {
        var name = "MyGarageODataPaging_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using (var db = new GarageContext(options))
            {
                await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.AI_KnowledgeBase (Id bigint NOT NULL PRIMARY KEY, Topic nvarchar(255) NOT NULL, Content nvarchar(max) NOT NULL, SourceUrl varchar(500) NULL, IsDynamic bit NULL, LastUpdated datetime2 NULL)");
                await db.Database.ExecuteSqlRawAsync("WITH n AS (SELECT TOP (240) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects) INSERT INTO dbo.AI_KnowledgeBase (Id, Topic, Content, IsDynamic) SELECT n, CASE WHEN n <= 120 THEN N'B' ELSE N'A' END, N'hidden', CASE WHEN n % 2 = 0 THEN 1 ELSE 0 END FROM n");
            }
            await using var factory = new TestWebApplicationFactory();
            await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
            using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiKnowledgeCatalogReader>();
                services.AddScoped(_ => new SqlCatalogContext(target.ConnectionString));
                services.AddScoped<IAiKnowledgeCatalogReader>(sp => new AiKnowledgeCatalogReader(sp.GetRequiredService<SqlCatalogContext>().Db));
            }));
            using var client = app.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
            using var first = await client.GetAsync("/odata/AiKnowledgeCatalog?$filter=IsDynamic%20eq%20true&$orderby=Topic%20desc,Id%20desc&$select=Id,Topic&$count=true");
            Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());
            using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
            Assert.Equal(120, firstJson.RootElement.GetProperty("@odata.count").GetInt32());
            var ids = firstJson.RootElement.GetProperty("value").EnumerateArray().Select(x => x.GetProperty("Id").GetInt64()).ToList();
            var next = firstJson.RootElement.GetProperty("@odata.nextLink").GetString();
            Assert.NotNull(next);
            using var second = await client.GetAsync(next);
            Assert.True(second.IsSuccessStatusCode, await second.Content.ReadAsStringAsync());
            using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
            ids.AddRange(secondJson.RootElement.GetProperty("value").EnumerateArray().Select(x => x.GetProperty("Id").GetInt64()));
            Assert.Equal(Enumerable.Range(1, 120).Select(i => (long)(i * 2)).Where(i => i <= 120).Reverse()
                .Concat(Enumerable.Range(1, 120).Select(i => (long)(i * 2)).Where(i => i > 120).Reverse()), ids);

            // Clients commonly sort by a non-unique field and omit the key.
            // The continuation must still traverse every matching row once.
            using var implicitKeyFirst = await client.GetAsync("/odata/AiKnowledgeCatalog?$filter=IsDynamic%20eq%20true&$orderby=Topic%20desc&$select=Id,Topic&$count=true");
            Assert.True(implicitKeyFirst.IsSuccessStatusCode, await implicitKeyFirst.Content.ReadAsStringAsync());
            using var implicitFirstJson = JsonDocument.Parse(await implicitKeyFirst.Content.ReadAsStringAsync());
            var implicitIds = implicitFirstJson.RootElement.GetProperty("value").EnumerateArray().Select(x => x.GetProperty("Id").GetInt64()).ToList();
            var implicitNext = implicitFirstJson.RootElement.GetProperty("@odata.nextLink").GetString();
            Assert.NotNull(implicitNext);
            using var implicitSecond = await client.GetAsync(implicitNext);
            Assert.True(implicitSecond.IsSuccessStatusCode, await implicitSecond.Content.ReadAsStringAsync());
            using var implicitSecondJson = JsonDocument.Parse(await implicitSecond.Content.ReadAsStringAsync());
            implicitIds.AddRange(implicitSecondJson.RootElement.GetProperty("value").EnumerateArray().Select(x => x.GetProperty("Id").GetInt64()));
            Assert.Equal(120, implicitIds.Count);
            Assert.Equal(120, implicitIds.Distinct().Count());
            Assert.Equal(Enumerable.Range(1, 120).Select(i => (long)(i * 2)), implicitIds.Order());

            using var malformed = await client.GetAsync("/odata/AiKnowledgeCatalog?$skiptoken=invalid");
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class SqlCatalogContext(string connectionString) : IDisposable
    {
        public GarageContext Db { get; } = new(new DbContextOptionsBuilder<GarageContext>().UseSqlServer(connectionString).Options);
        public void Dispose() => Db.Dispose();
    }
}
