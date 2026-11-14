using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyGarage.Infrastructure.Ai;
using System.Data.Common;
using Xunit;

namespace MyGarage.Tests;

public sealed class AiModelConfigPageSqlTests
{
    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [LocalSqlFact]
    public async Task Reader_PagesAndPreservesWhitespaceKeyFlag_WithoutReturningKey()
    {
        var name = "MyGarageAiRead_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<AiModelConfigReadDbContext>().UseSqlServer(target.ConnectionString).Options;
            await using var db = new AiModelConfigReadDbContext(options);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.AI_ModelConfigs (Id bigint NOT NULL PRIMARY KEY, Provider varchar(50) NOT NULL, ApiKey varchar(255) NULL, ModelName varchar(100) NOT NULL, MaxTokens int NULL, Temperature decimal(3,2) NULL, IsActive bit NULL)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.AI_ModelConfigs (Id, Provider, ApiKey, ModelName) VALUES (1, 'A', NULL, 'm'), (2, 'B', '   ', 'm'), (3, 'C', 'secret-value', 'm')");
            var reader = new AiModelConfigPageReader(db);
            var first = await reader.ReadAsync(1, 2, default);
            Assert.Equal(3, first.Total);
            Assert.Equal(new long[] { 1, 2 }, first.Items.Select(x => x.Id));
            Assert.All(first.Items, x => Assert.False(x.HasApiKey));
            var second = await reader.ReadAsync(2, 2, default);
            Assert.True(Assert.Single(second.Items).HasApiKey);
            Assert.Empty((await reader.ReadAsync(int.MaxValue, 100, default)).Items);
            await Assert.ThrowsAsync<NotSupportedException>(() => db.SaveChangesAsync());
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.AI_SmartDispatching (Id bigint NOT NULL PRIMARY KEY, BranchId bigint NOT NULL, Date date NOT NULL, Recommendation nvarchar(max) NULL, EfficiencyScore decimal(5,2) NULL, Status varchar(50) NULL, CreatedAt datetime2 NULL)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.AI_SmartDispatching (Id, BranchId, Date, Recommendation) VALUES (1, 1, '2026-01-01', N'Gợi ý'), (2, 2, '2026-01-02', NULL)");
            var dispatchCounter = new CommandCounter();
            await using var dispatchDb = new DispatchReadDbContext(new DbContextOptionsBuilder<DispatchReadDbContext>().UseSqlServer(target.ConnectionString).AddInterceptors(dispatchCounter).Options);
            var dispatch = await new DispatchListReader(dispatchDb).ReadAsync(2, 1, default);
            Assert.Equal(2, dispatch.Total);
            Assert.Equal(2, Assert.Single(dispatch.Items).Id);
            dispatchCounter.Count = 0;
            var allDispatch = await new DispatchListReader(dispatchDb).ReadAsync(null, null, default);
            Assert.Equal(2, allDispatch.Total);
            Assert.Equal(new long[] { 1, 2 }, allDispatch.Items.Select(x => x.Id));
            Assert.Equal(1, dispatchCounter.Count);
            Assert.Empty((await new DispatchListReader(dispatchDb).ReadAsync(int.MaxValue, 100, default)).Items);
            await Assert.ThrowsAsync<NotSupportedException>(() => dispatchDb.SaveChangesAsync());
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.AI_Forecasts (Id bigint NOT NULL PRIMARY KEY, VehicleId bigint NOT NULL, Type varchar(50) NOT NULL, PredictedDate date NOT NULL, ConfidenceScore decimal(5,2) NULL, CreatedAt datetime2 NULL)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.AI_Forecasts (Id, VehicleId, Type, PredictedDate) VALUES (1, 10, 'SERVICE', '2026-01-01'), (2, 20, 'SERVICE', '2026-01-02')");
            var forecastCounter = new CommandCounter();
            await using var forecastDb = new ForecastReadDbContext(new DbContextOptionsBuilder<ForecastReadDbContext>().UseSqlServer(target.ConnectionString).AddInterceptors(forecastCounter).Options);
            var forecasts = await new MyGarage.Infrastructure.Ai.ForecastListReader(forecastDb).ReadAsync(2, 1, default);
            Assert.Equal(2, forecasts.Total);
            Assert.Equal(2, Assert.Single(forecasts.Items).Id);
            forecastCounter.Count = 0;
            var allForecasts = await new MyGarage.Infrastructure.Ai.ForecastListReader(forecastDb).ReadAsync(null, null, default);
            Assert.Equal(2, allForecasts.Total);
            Assert.Equal(new long[] { 1, 2 }, allForecasts.Items.Select(x => x.Id));
            Assert.Equal(1, forecastCounter.Count);
            await Assert.ThrowsAsync<NotSupportedException>(() => forecastDb.SaveChangesAsync());
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
