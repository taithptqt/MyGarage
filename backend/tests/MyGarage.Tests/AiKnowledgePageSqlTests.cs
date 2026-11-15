using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Infrastructure.Ai;
using Xunit;

namespace MyGarage.Tests;

public sealed class AiKnowledgePageSqlTests
{
    [LocalSqlFact]
    public async Task Reader_PagesExistingRows_AndDoesNotPermitWrites()
    {
        var name = "MyGarageKnowledge_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using var db = new GarageContext(options);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.AI_KnowledgeBase (Id bigint NOT NULL PRIMARY KEY, Topic nvarchar(255) NOT NULL, Content nvarchar(max) NOT NULL, SourceUrl varchar(500) NULL, IsDynamic bit NULL, LastUpdated datetime2 NULL)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.AI_KnowledgeBase (Id, Topic, Content, IsDynamic) VALUES (1, N'Bảo dưỡng', N'Nội dung 1', 1), (2, N'Xe', N'Nội dung 2', NULL)");
            var reader = new AiKnowledgePageReader(db);
            var first = await reader.ReadAsync(1, 1, default);
            Assert.Equal(2, first.Total);
            Assert.Equal("Bảo dưỡng", Assert.Single(first.Items).Topic);
            Assert.Equal(2, Assert.Single((await reader.ReadAsync(2, 1, default)).Items).Id);
            Assert.Empty((await reader.ReadAsync(int.MaxValue, 100, default)).Items);
            var metadata = await new AiKnowledgeCatalogReader(db).Query()
                .Where(x => x.IsDynamic == true && x.Topic.Contains("dưỡng"))
                .OrderBy(x => x.Id).Take(1).ToListAsync();
            Assert.Equal(1, Assert.Single(metadata).Id);
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
