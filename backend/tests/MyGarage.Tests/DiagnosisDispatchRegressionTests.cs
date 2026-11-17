using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyGarage.Data;
using MyGarage.Infrastructure.Persistence.Stores.Workshop;
using MyGarage.Interfaces;
using MyGarage.Middleware;
using MyGarage.Infrastructure.Queries;
using Xunit;

namespace MyGarage.Tests;

public sealed class DiagnosisDispatchRegressionTests
{
    [Fact]
    public async Task AttachmentUploads_AreNotRejectedAsDuplicateBySize()
    {
        var guard = new AlwaysRejectGuard();
        var called = 0;
        var middleware = new WriteRequestSpamGuardMiddleware(_ => { called++; return Task.CompletedTask; }, guard);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/vehicle-condition-logs/attachments";
        context.Request.ContentType = "multipart/form-data; boundary=abc";
        await middleware.Invoke(context);
        Assert.Equal(1, called);
        context.Request.Path = "/api/v1/vehicle-condition-logs";
        context.Response.Body = new MemoryStream();
        await middleware.Invoke(context);
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
    }

    [LocalSqlFact]
    public async Task PendingSupplementary_TranslatesLatestDiagnosisQueryOnSqlServer()
    {
        var name = "MyGarageDiagnosisPending_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand()) { command.CommandText = $"CREATE DATABASE [{name}]"; await command.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using var db = new GarageContext(options);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE dbo.ServiceOrders (Id bigint PRIMARY KEY, ExecuteBranchId bigint, OriginBranchId bigint);
                CREATE TABLE dbo.Diagnoses (Id bigint PRIMARY KEY, ServiceOrderId bigint, Status varchar(30), UpdatedAt datetime2 NULL, CreatedAt datetime2 NULL);
                CREATE TABLE dbo.DiagnosisRecommendations (Id bigint PRIMARY KEY, DiagnosisId bigint, DiagnosisAssignmentId bigint NULL, RecommendationStatus varchar(40), UpdatedAt datetime2 NULL, CreatedAt datetime2 NULL);
                CREATE TABLE dbo.Quotations (Id bigint PRIMARY KEY, ServiceOrderId bigint, DiagnosisAssignmentId bigint NULL, DiagnosisId bigint NULL, VersionNo int, Status varchar(40), CreatedAt datetime2 NULL);
                CREATE TABLE dbo.QuotationDetails (Id bigint PRIMARY KEY, QuotationId bigint, DiagnosisRecommendationId bigint NULL);
                INSERT INTO dbo.ServiceOrders VALUES (1,1,1);
                INSERT INTO dbo.Diagnoses VALUES (10,1,'COMPLETED','2026-01-01','2026-01-01');
                INSERT INTO dbo.DiagnosisRecommendations VALUES (20,10,NULL,'ACTIVE','2026-01-02','2026-01-02');
                """);
            var result = await new EfDiagnosisRecommendationDataService(db).GetPendingSupplementaryAsync(null, 1);
            Assert.Single(result);
            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.Quotations VALUES (1,1,NULL,10,1,'DRAFT','2026-01-03'); INSERT INTO dbo.QuotationDetails VALUES (1,1,20);");
            Assert.Empty(await new EfDiagnosisRecommendationDataService(db).GetPendingSupplementaryAsync(null, 1));
        }
        finally
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
        }
    }

    [LocalSqlFact]
    public async Task ActiveDiagnosisBays_SortsOnSqlServerBeforeProjection()
    {
        var name = "MyGarageDiagnosisBays_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand()) { command.CommandText = $"CREATE DATABASE [{name}]"; await command.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using var db = new GarageContext(options);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.Bays (Id bigint NOT NULL PRIMARY KEY, BranchId bigint NOT NULL, IsDeleted bit NULL, IsActive bit NULL, BayType varchar(40), Code varchar(40), Name nvarchar(100)); INSERT INTO dbo.Bays VALUES (1,1,0,1,'SP_DIAGNOSE','B','B'),(2,1,0,1,'SP_DIAGNOSE','A','A'),(3,2,0,1,'SP_DIAGNOSE','C','C');");
            var store = new EfDiagnosisStore(db, new ConfigurationBuilder().Build());
            var bays = await store.GetActiveDiagnoseBaysAsync(1, "SP_DIAGNOSE");
            Assert.Equal(new long[] { 2, 1 }, bays.Select(x => x.Id));
        }
        finally
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class AlwaysRejectGuard : IWriteRequestSpamGuard
    {
        public bool IsCoolingDown(string key, DateTime nowUtc) => true;
        public bool TryEnter(string key, DateTime nowUtc, out string? rejectReason) { rejectReason = null; return false; }
        public void Exit(string key, DateTime nowUtc) { }
    }
}
