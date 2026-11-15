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

public sealed class SalaryRequestPagingSqlTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [LocalSqlFact]
    public async Task Requests_PageAndCountsFollowStatusAndBranchFilters()
    {
        var name = "MyGarageSalaryRequest_" + Guid.NewGuid().ToString("N");
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
                CREATE TABLE dbo.finance_approval_requests (id bigint PRIMARY KEY, request_type nvarchar(60), request_payload nvarchar(max),
                    status nvarchar(30), amount decimal(18,2), reason nvarchar(100), reject_reason nvarchar(100),
                    created_at datetime2, updated_at datetime2, requested_by bigint, approver_id bigint);
                INSERT dbo.Branches VALUES (1, N'One'), (2, N'Two');
                INSERT dbo.finance_approval_requests (id,request_type,request_payload,status,created_at) VALUES
                    (1,'PAYROLL_ADJUSTMENT',N'{{"requestSubtype":"STAFF_INITIAL_SALARY","branchId":1}}','PENDING','2026-01-01'),
                    (2,'PAYROLL_ADJUSTMENT',N'{{"requestSubtype":"STAFF_INITIAL_SALARY","branchId":1}}','APPROVED','2026-01-01'),
                    (3,'PAYROLL_ADJUSTMENT',N'{{"requestSubtype":"STAFF_INITIAL_SALARY","branchId":1}}','REJECTED','2026-01-01'),
                    (4,'PAYROLL_ADJUSTMENT',N'{{"requestSubtype":"STAFF_INITIAL_SALARY","branchId":2}}','PENDING','2026-01-01'),
                    (5,'PAYROLL_ADJUSTMENT',N'{{"requestSubtype":"OTHER","branchId":1}}','PENDING','2026-01-01');
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
            var legacy = Assert.IsType<OkObjectResult>(await controller.GetInitialSalaryRequests("ALL", 1));
            using var legacyDoc = JsonSerializer.SerializeToDocument(legacy.Value, WebJson);
            var legacyItems = legacyDoc.RootElement.ValueKind == JsonValueKind.Object
                ? legacyDoc.RootElement.GetProperty("items")
                : legacyDoc.RootElement;
            Assert.Equal(3, legacyItems.GetArrayLength());

            var paged = Assert.IsType<OkObjectResult>(await controller.GetInitialSalaryRequests("ALL", 1, 2, 1));
            using var pagedDoc = JsonSerializer.SerializeToDocument(paged.Value, WebJson);
            Assert.Equal(2L, Assert.Single(pagedDoc.RootElement.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
            Assert.Equal(3, pagedDoc.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, pagedDoc.RootElement.GetProperty("pendingCount").GetInt32());
            Assert.Equal(1, pagedDoc.RootElement.GetProperty("approvedCount").GetInt32());
            Assert.False(http.Response.Headers.ContainsKey("X-Total-Count"));

            var pending = Assert.IsType<OkObjectResult>(await controller.GetInitialSalaryRequests("PENDING", 1, 1, 10));
            using var pendingDoc = JsonSerializer.SerializeToDocument(pending.Value, WebJson);
            Assert.Equal(1, pendingDoc.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, pendingDoc.RootElement.GetProperty("pendingCount").GetInt32());
            Assert.Equal(0, pendingDoc.RootElement.GetProperty("approvedCount").GetInt32());

            var empty = Assert.IsType<OkObjectResult>(await controller.GetInitialSalaryRequests("ALL", 1, int.MaxValue, 100));
            using var emptyDoc = JsonSerializer.SerializeToDocument(empty.Value, WebJson);
            Assert.Empty(emptyDoc.RootElement.GetProperty("items").EnumerateArray());

            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Staff"), new Claim("role_code", "MANAGER") }, "test"));
            Assert.IsType<ForbidResult>(await controller.GetInitialSalaryRequests("ALL", 1, 1, 1));
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
