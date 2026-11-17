using Microsoft.Extensions.DependencyInjection;
using MyGarage.Interfaces;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class AppointmentDeferredHoldCapacityTests
{
    [Fact]
    public async Task FlexibleCapacity_DoesNotCountProcessedHoldAndItsAppointmentTwice()
    {
        await using var factory = new TestWebApplicationFactory();
        var start = DateTime.Today.AddDays(3).AddHours(9);
        var end = start.AddHours(1);
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Bays.AddRange(Enumerable.Range(1, 3).Select(i => new Bay
            {
                Id = i, BranchId = 1, Code = $"B-{i}", Name = $"Bay {i}",
                BayType = "GENERAL", ServiceArea = "GENERAL", IsActive = true
            }));
            db.Appointments.Add(new Appointment { Id = 901, CustomerId = 100, BranchId = 1, StartTime = start, EndTime = end, Status = "PENDING" });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var holds = scope.ServiceProvider.GetRequiredService<ISlotHoldService>();
        var selection = scope.ServiceProvider.GetRequiredService<IAppointmentBaySelectionService>();
        var first = holds.CreateHold(1, start, end, "FLEXIBLE", null, null, 1);
        var current = holds.CreateHold(1, start, end, "FLEXIBLE", null, null, 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => selection.EnsureFlexibleQueueCapacityAsync(1, start, end, current.Token));
        await selection.EnsureFlexibleQueueCapacityAsync(1, start, end, current.Token, new[] { first.Token });
        Assert.True(holds.TryGetActiveHold(first.Token, out _));
        Assert.True(holds.TryGetActiveHold(current.Token, out _));
    }
}