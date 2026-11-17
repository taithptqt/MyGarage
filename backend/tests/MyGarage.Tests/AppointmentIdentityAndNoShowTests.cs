using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class AppointmentIdentityAndNoShowTests
{
    [Fact]
    public async Task UnverifiedPublicBookingAndMismatchedCustomer_AreRejectedWithoutChanges()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Customers.Add(new Customer { Id = 101, FullName = "Victim", Phone = "0901234567", Type = "INDIVIDUAL", PasswordHash = "hash", SecurityStamp = "test-stamp", IsActive = true });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        using var guest = await client.PostAsJsonAsync("/api/v1/appointments/public-bookings", new { branchId = 1, customerId = 101, customerFullName = "Changed", customerPhone = "0909999999" });
        Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        using var single = await client.PostAsJsonAsync("/api/v1/appointments", new { branchId = 1, customerId = 101, customerFullName = "Changed", customerPhone = "0909999999" });
        Assert.Equal(HttpStatusCode.Forbidden, single.StatusCode);
        using var group = await client.PostAsJsonAsync("/api/v1/group-sessions", new { branchId = 1, items = new[] { new { branchId = 1, customerId = 101, customerFullName = "Changed", customerPhone = "0909999999" } } });
        Assert.Equal(HttpStatusCode.BadRequest, group.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var victim = await db.Customers.AsNoTracking().SingleAsync(x => x.Id == 101);
        Assert.Equal("Victim", victim.FullName);
        Assert.Equal("0901234567", victim.Phone);
        Assert.Empty(await db.Appointments.ToListAsync());
        Assert.Empty(await db.GroupSessions.ToListAsync());
    }

    [Theory]
    [InlineData("999999")]
    [InlineData("abc")]
    [InlineData("0")]
    public async Task Booking_WithInvalidServiceIds_IsRejectedBeforeCreatingAppointment(string ids)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        using var response = await client.PostAsJsonAsync("/api/v1/appointments", new
        {
            branchId = 1, customerFullName = "Customer", customerPhone = "0901234567",
            requestedServiceIds = ids, bookingFlow = "FIXED", startTime = DateTime.Now.Date.AddDays(3).AddHours(9),
            vehicleModelId = 1, licensePlate = "51A-12345", manufactureYear = 2020
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(System.Text.Json.JsonDocument.Parse(body).RootElement.TryGetProperty("message", out var message) && message.GetString()!.Contains("dịch vụ", StringComparison.OrdinalIgnoreCase), body);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Empty(await db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task LateCheckIn_MarksNoShowAndReleasesWorkSlotAndParts()
    {
        await using var factory = new TestWebApplicationFactory();
        var start = new DateTime(2026, 1, 1, 9, 0, 0);
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Appointments.Add(new Appointment { Id = 701, CustomerId = 100, BranchId = 1, Status = "CONFIRMED", StartTime = start, EndTime = start.AddHours(1), RequestedServiceIds = "1" });
            db.WorkSlots.Add(new WorkSlot { Id = 702, AppointmentId = 701, BranchId = 1, Status = "RESERVED", SlotType = "REPAIR", StartTime = start, EndTime = start.AddHours(1) });
            db.Products.Add(new Product { Id = 703, Name = "Part", Sku = "P703", Unit = "piece" });
            db.Warehouses.Add(new Warehouse { Id = 704, BranchId = 1, Code = "W704", Name = "Warehouse", Type = "GENERAL", IsActive = true });
            db.Inventories.Add(new Inventory { Id = 705, WarehouseId = 704, ProductId = 703, Quantity = 10, ReservedQuantity = 3 });
            db.PartInventoryReservations.Add(new PartInventoryReservation { Id = 706, WorkSlotId = 702, ProductId = 703, Status = "RESERVED", ReservedQuantity = 3, ExpiryTime = start.AddDays(3) });
            return Task.CompletedTask;
        });
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IAppointmentLifecycleService>();
            var result = await service.CheckInAsync(701, start.AddDays(2), 200);
            Assert.Equal("NO_SHOW", result!.Status);
        }
        using var verifyScope = factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Equal("CANCELED", (await verify.WorkSlots.SingleAsync(x => x.Id == 702)).Status);
        Assert.Equal("CANCELED", (await verify.PartInventoryReservations.SingleAsync(x => x.Id == 706)).Status);
        Assert.Equal(0, (await verify.Inventories.SingleAsync(x => x.Id == 705)).ReservedQuantity);
        Assert.Equal(1, (await verify.Customers.SingleAsync(x => x.Id == 100)).NoShowCount);
        Assert.Empty(await verify.ServiceOrders.Where(x => x.AppointmentId == 701).ToListAsync());
    }
}