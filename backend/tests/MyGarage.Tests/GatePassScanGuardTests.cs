using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class GatePassScanGuardTests
{
    [Fact]
    public async Task GenericCreateAndUpdate_CannotForgeScanConfirmation()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceOrders.Add(new ServiceOrder { Id = 8811, Code = "SO-GATE", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "READY" });
            db.GatePasses.Add(new GatePass { Id = 8812, ServiceOrderId = 8811, QrCode = "GP-8812", IssuedBy = 201, ClearanceType = "NORMAL", Status = "ISSUED" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        using var create = await client.PostAsJsonAsync("/api/v1/gate-passes", new { serviceOrderId = 8811, issuedBy = 201, status = "SCANNED" });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        using var update = await client.PutAsJsonAsync("/api/v1/gate-passes/8812", new { serviceOrderId = 8811, issuedBy = 201, status = "PENDING", passedAt = new DateTime(2026, 1, 1) });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var gatePass = Assert.Single(await db.GatePasses.ToListAsync());
        Assert.Equal("ISSUED", gatePass.Status);
        Assert.Null(gatePass.PassedAt);
        using var scan = await client.PostAsJsonAsync("/api/v1/gate-passes/8812/scans", new { isOldPartsChecked = true });
        Assert.True(scan.StatusCode == HttpStatusCode.OK, await scan.Content.ReadAsStringAsync());
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GarageContext>();
        var scanned = await verifyDb.GatePasses.AsNoTracking().SingleAsync(x => x.Id == 8812);
        Assert.Equal("SCANNED", scanned.Status);
        Assert.Equal(201, scanned.ScannedBy);
        Assert.NotNull(scanned.PassedAt);
    }
}