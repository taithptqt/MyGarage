using MyGarage.DTOs.Responses;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class QcHandoverLifecycleTests
{
    [Fact]
    public async Task Handover_IgnoresCancelledWork_IsIdempotent_AndPersistsNotificationAndReminder()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "B1", Name = "Brand" });
            db.VehicleModels.Add(new VehicleModel { Id = 1, BrandId = 1, ModelName = "Model" });
            db.Vehicles.Add(new Vehicle { Id = 1, ModelId = 1, LicensePlate = "TEST-1" });
            db.ServiceOrders.Add(new ServiceOrder { Id = 8701, Code = "SO-HANDOVER", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "READY", PaymentStatus = "PAID", Qcstatus = "PASS" });
            db.ServiceOrderDetails.AddRange(
                new ServiceOrderDetail { Id = 8711, ServiceOrderId = 8701, ItemType = "LABOR", ItemName = "Done", Status = "COMPLETED", Unit = "job", PayerType = "CUSTOMER", ServiceSource = "INITIAL" },
                new ServiceOrderDetail { Id = 8712, ServiceOrderId = 8701, ItemType = "SERVICE", ItemName = "Canceled", Status = "CANCELED", Unit = "job", PayerType = "CUSTOMER", ServiceSource = "INITIAL" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        var firstTime = new DateTime(2026, 1, 1, 12, 0, 0);
        using var first = await client.PostAsJsonAsync("/api/v1/service-orders/8701/handovers", new HandoverRequestDto { HandedOverBy = 201, ReceiverName = "Customer", ReceiverPhone = "0901234567", Notes = "Biên bản đã ký", HandoverTime = firstTime });
        Assert.True(first.StatusCode == HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        using var second = await client.PostAsJsonAsync("/api/v1/service-orders/8701/handovers", new HandoverRequestDto { HandedOverBy = 201, ReceiverName = "Other", ReceiverPhone = "0909999999", Notes = "Ignored", HandoverTime = firstTime.AddDays(1) });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var order = await db.ServiceOrders.SingleAsync(x => x.Id == 8701);
        Assert.Equal(firstTime, order.ActualHandover);
        Assert.Equal("Customer", order.HandoverReceiverName);
        Assert.Equal("0901234567", order.HandoverReceiverPhone);
        Assert.Equal("Biên bản đã ký", order.HandoverNotes);
        var detail = await client.GetFromJsonAsync<ServiceOrderResponseDto>("/api/v1/service-orders/8701");
        Assert.Equal("Customer", detail!.HandoverReceiverName);
        Assert.Equal("0901234567", detail.HandoverReceiverPhone);
        Assert.Equal("Biên bản đã ký", detail.HandoverNotes);
        Assert.Single(await db.Notifications.Where(x => x.CustomerId == 100 && x.LinkAction!.Contains("serviceOrderId=8701")).ToListAsync());
        Assert.Single(await db.MaintenanceReminders.Where(x => x.ServiceOrderId == 8701).ToListAsync());
    }

    [Fact]
    public async Task Handover_RejectsOversizedReceiverWithoutClosingOrder()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceOrders.Add(new ServiceOrder { Id = 8791, Code = "SO-LONG", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "READY", PaymentStatus = "PAID", Qcstatus = "PASS" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        using var response = await client.PostAsJsonAsync("/api/v1/service-orders/8791/handovers", new HandoverRequestDto { HandedOverBy = 201, ReceiverName = new string('A', 256) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var order = await db.ServiceOrders.AsNoTracking().SingleAsync(x => x.Id == 8791);
        Assert.Equal("READY", order.Status);
        Assert.Null(order.ActualHandover);
        Assert.Null(order.HandoverReceiverName);
    }

    [Theory]
    [InlineData("passes")]
    [InlineData("failures")]
    public async Task QcDecision_OnClosedOrder_DoesNotReopenOrModifyInspection(string action)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceOrders.Add(new ServiceOrder { Id = 8801, Code = "SO-CLOSED", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "CLOSED", PaymentStatus = "PAID", Qcstatus = "PASS" });
            db.QcInspections.Add(new QcInspection { Id = 8802, ServiceOrderId = 8801, QcStaffId = 201, Status = "PENDING" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        using var response = await client.PostAsJsonAsync($"/api/v1/qc-inspections/8802/{action}", new { inspectorId = 201, notes = "x", reason = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Equal("CLOSED", (await db.ServiceOrders.SingleAsync(x => x.Id == 8801)).Status);
        Assert.Equal("PENDING", (await db.QcInspections.SingleAsync(x => x.Id == 8802)).Status);
    }
}