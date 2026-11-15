using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Infrastructure.Ai;
using Xunit;

namespace MyGarage.Tests;

public sealed class DispatchBayCandidatesSqlTests
{
    [LocalSqlFact]
    public async Task Candidates_UseExactStatusAndLimitOnSqlServer()
    {
        var name = "MyGarageBayCandidates_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using var db = new GarageContext(options);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.Bays (Id bigint NOT NULL PRIMARY KEY, BranchId bigint NOT NULL, Code nvarchar(50) NOT NULL, Name nvarchar(100) NOT NULL, SequenceOrder int NOT NULL, OperationalStatus varchar(50) NULL, IsActive bit NULL, IsDeleted bit NULL)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.Bays (Id,BranchId,Code,Name,SequenceOrder,OperationalStatus,IsActive,IsDeleted) VALUES (1,1,'B1','Bay',1,'AVAILABLE',1,0),(2,1,'B2','Bay',2,'AVAILABLE',1,0),(3,1,'B3','Bay',3,'AVAILABLE',1,0),(4,1,'B4','Bay',4,'AVAILABLE',1,0),(5,1,'B5','Bay',0,'available',1,0),(6,1,'B6','Bay',0,'AVAILABLE ',1,0),(7,2,'B7','Bay',0,'AVAILABLE',1,0)");
            var rows = await new DispatchBayCandidates(db).GetAvailableAsync(1);
            Assert.Equal(new long[] { 1, 2, 3 }, rows.Select(x => x.Id));
            Assert.Empty(await new DispatchBayCandidates(db).GetAvailableAsync(999));
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
