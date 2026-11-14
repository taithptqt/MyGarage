using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class PayrollPeriodGuardTests
{
    [Fact]
    public async Task ManualRun_RejectsClosedPeriodWithoutWritingRun()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.PayrollPeriods.Add(new PayrollPeriod { Id = 1, PeriodCode = "2026-01", Status = "CLOSED",
                StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31), PayrollDate = new DateOnly(2026, 1, 31) });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var result = await client.PostAsJsonAsync("/api/payroll-runs", new { branchId = 1, period = "2026-01", details = new[] { new { staffId = 200, baseSalary = 100m } } });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Empty(await db.PayrollRuns.ToListAsync());
        Assert.Equal("CLOSED", (await db.PayrollPeriods.SingleAsync()).Status);
    }
}
