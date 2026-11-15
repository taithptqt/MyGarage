using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Controllers;
using MyGarage.Data;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class SalaryProfilePagingSqlTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [LocalSqlFact]
    public async Task Profiles_PagingPreservesLegacyRowsAndFiltersBeforeCount()
    {
        var name = "MyGarageSalaryPage_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true };
        var test = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name, Pooling = false };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(test.ConnectionString).Options;
            await using var db = new GarageContext(options);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE dbo.Staffs (Id bigint PRIMARY KEY, FullName nvarchar(100));
                CREATE TABLE dbo.Branches (Id bigint PRIMARY KEY, Name nvarchar(100));
                CREATE TABLE dbo.staff_salary_profiles (id bigint PRIMARY KEY, staff_id bigint, branch_id bigint,
                    base_salary decimal(18,2), effective_from datetime2, effective_to datetime2, status nvarchar(30),
                    note nvarchar(100), updated_by bigint, created_at datetime2, updated_at datetime2);
                INSERT dbo.Staffs VALUES (1, N'Staff');
                INSERT dbo.Branches VALUES (1, N'Branch 1'), (2, N'Branch 2');
                INSERT dbo.staff_salary_profiles (id,staff_id,branch_id,base_salary,created_at)
                    VALUES (1,1,1,100,'2026-01-01'),(2,1,1,200,'2026-01-01'),(3,1,2,300,'2026-01-01');
                """);
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.Role, "Staff"), new Claim("role_code", "ADMIN") }, "test")) };
            var profileQuery = new MyGarage.Application.Payroll.SalaryProfileListQuery(new MyGarage.Infrastructure.Payroll.SalaryProfileListReader(db));
            var crud = new AccountingCrudService(db);
            var controller = new SalaryAdminController(
                new MyGarage.Application.Payroll.SalaryAdminCommandFacade(
                    crud,
                    crud,
                    crud,
                    new MyGarage.Infrastructure.Queries.EfSalaryAdminDataService(db, crud),
                    profileQuery))
                { ControllerContext = new ControllerContext { HttpContext = http } };
            var legacy = Assert.IsType<OkObjectResult>(await controller.GetSalaryProfiles(1));
            using var legacyDoc = JsonSerializer.SerializeToDocument(legacy.Value, WebJson);
            var legacyItems = legacyDoc.RootElement.ValueKind == JsonValueKind.Object
                ? legacyDoc.RootElement.GetProperty("items")
                : legacyDoc.RootElement;
            Assert.Equal(2, legacyItems.GetArrayLength());

            var paged = Assert.IsType<OkObjectResult>(await controller.GetSalaryProfiles(1, 2, 1));
            using var pagedDoc = JsonSerializer.SerializeToDocument(paged.Value, WebJson);
            Assert.Equal(2, pagedDoc.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal(2, pagedDoc.RootElement.GetProperty("page").GetInt32());
            Assert.Equal(1L, Assert.Single(pagedDoc.RootElement.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
            Assert.False(http.Response.Headers.ContainsKey("X-Total-Count"));

            var empty = Assert.IsType<OkObjectResult>(await controller.GetSalaryProfiles(1, int.MaxValue, 100));
            using var emptyDoc = JsonSerializer.SerializeToDocument(empty.Value, WebJson);
            Assert.Empty(emptyDoc.RootElement.GetProperty("items").EnumerateArray());

            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Staff"), new Claim("role_code", "MANAGER") }, "test"));
            Assert.IsType<ForbidResult>(await controller.GetSalaryProfiles(1, 1, 1));
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
