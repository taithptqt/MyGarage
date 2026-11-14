using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.Application.Ai;
using MyGarage.Infrastructure.Ai;
using Xunit;

namespace MyGarage.Tests;

public sealed class AiKnowledgeCatalogODataTests
{
    [Fact]
    public async Task Catalog_RequiresAdminAndSupportsBoundedQueryWithoutContent()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiKnowledgeCatalogReader>();
            services.AddScoped<IAiKnowledgeCatalogReader, FakeReader>();
        }));
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/odata/AiKnowledgeCatalog")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/odata/AiKnowledgeCatalog")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        var result = await client.GetAsync("/odata/AiKnowledgeCatalog?$filter=IsDynamic%20eq%20true&$orderby=Id%20desc&$top=1&$count=true");
        Assert.True(result.StatusCode == HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        var root = body.RootElement;
        var items = root.ValueKind == JsonValueKind.Array ? root : root.GetProperty("value");
        var row = Assert.Single(items.EnumerateArray());
        Assert.Equal(2, row.GetProperty("Id").GetInt64());
        Assert.False(row.TryGetProperty("Content", out _));
        Assert.False(row.TryGetProperty("SourceUrl", out _));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/odata/AiKnowledgeCatalog?$top=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/odata/AiKnowledgeCatalog?$expand=Content")).StatusCode);
    }

    [LocalSqlFact]
    public async Task Catalog_QueriesSqlThroughRegisteredInfrastructureReader()
    {
        var name = "MyGarageKnowledgeHttp_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<AiKnowledgeReadDbContext>().UseSqlServer(target.ConnectionString).Options;
            await using (var db = new AiKnowledgeReadDbContext(options))
            {
                await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.AI_KnowledgeBase (Id bigint NOT NULL PRIMARY KEY, Topic nvarchar(255) NOT NULL, Content nvarchar(max) NOT NULL, SourceUrl varchar(500) NULL, IsDynamic bit NULL, LastUpdated datetime2 NULL)");
                await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.AI_KnowledgeBase (Id, Topic, Content, SourceUrl, IsDynamic) VALUES (1, N'First', N'secret', 'private', 0), (2, N'Second', N'secret', 'private', 1), (3, N'Third', N'secret', 'private', 1)");
            }
            await using var factory = new TestWebApplicationFactory();
            await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
            using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AiKnowledgeReadDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AiKnowledgeReadDbContext>>();
                services.RemoveAll<AiKnowledgeReadDbContext>();
                services.AddDbContext<AiKnowledgeReadDbContext>(options => options.UseSqlServer(target.ConnectionString));
            }));
            using var client = app.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
            var result = await client.GetAsync("/odata/AiKnowledgeCatalog?$filter=IsDynamic%20eq%20true&$orderby=Id%20desc&$top=1&$count=true");
            Assert.True(result.StatusCode == HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
            Assert.Equal(2, json.RootElement.GetProperty("@odata.count").GetInt32());
            var row = Assert.Single(json.RootElement.GetProperty("value").EnumerateArray());
            Assert.Equal(3, row.GetProperty("Id").GetInt64());
            Assert.False(row.TryGetProperty("Content", out _));
            Assert.False(row.TryGetProperty("SourceUrl", out _));
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class FakeReader : IAiKnowledgeCatalogReader
    {
        public IQueryable<AiKnowledgeCatalogRow> Query() => new[]
        {
            new AiKnowledgeCatalogRow { Id = 1, Topic = "First", IsDynamic = false },
            new AiKnowledgeCatalogRow { Id = 2, Topic = "Second", IsDynamic = true }
        }.AsQueryable();
    }
}
