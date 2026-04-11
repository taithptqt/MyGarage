using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyGarage.Data;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Options;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class CustomerAutomationJobProcessorTests
{
    [Fact]
    public async Task RunProactiveMaintenanceOutreachAsync_RepeatedRun_DoesNotCreateDuplicateNotifications()
    {
        var now = new DateTime(2026, 4, 8, 8, 0, 0, DateTimeKind.Utc);
        await using var context = CreateContext(nameof(RunProactiveMaintenanceOutreachAsync_RepeatedRun_DoesNotCreateDuplicateNotifications));
        SeedCustomer(context, 101, "Customer Maintenance");

        context.Vehicles.Add(new Vehicle
        {
            Id = 1001,
            LicensePlate = "51A-10001",
            ModelId = 1,
            Odo = 24_500,
            IsDeleted = false
        });
        context.CustomerVehicles.Add(new CustomerVehicle
        {
            Id = 9001,
            CustomerId = 101,
            VehicleId = 1001,
            IsCurrentOwner = true,
            IsDeleted = false
        });
        context.AiForecasts.Add(new AiForecast
        {
            Id = 7001,
            VehicleId = 1001,
            Type = "MAINTENANCE",
            PredictedDate = DateOnly.FromDateTime(now.Date.AddDays(10))
        });

        await context.SaveChangesAsync();

        var sut = CreateSut(context, now);

        var first = await sut.RunProactiveMaintenanceOutreachAsync();
        var second = await sut.RunProactiveMaintenanceOutreachAsync();

        Assert.Equal(1, first.NotifiedCount);
        Assert.Equal(0, second.NotifiedCount);
        Assert.True(second.SkippedCount >= 1);

        var notifications = await context.Notifications
            .Where(n => n.CustomerId == 101)
            .ToListAsync();
        Assert.Single(notifications);
        Assert.Contains("/jobs/maintenance/1001/forecast-7001", notifications[0].LinkAction);
    }

    [Fact]
    public async Task RunPostServiceSurveyAsync_RepeatedRun_DoesNotCreateDuplicateSurveyNotifications()
    {
        var now = new DateTime(2026, 4, 8, 8, 0, 0, DateTimeKind.Utc);
        await using var context = CreateContext(nameof(RunPostServiceSurveyAsync_RepeatedRun_DoesNotCreateDuplicateSurveyNotifications));
        SeedCustomer(context, 102, "Customer Survey");

        context.ServiceOrders.Add(new ServiceOrder
        {
            Id = 2001,
            Code = "SO-2001",
            CustomerId = 102,
            VehicleId = 5001,
            OriginBranchId = 1,
            ExecuteBranchId = 1,
            Status = nameof(ServiceOrderLifecycleStatus.CLOSED),
            ActualHandover = now.AddDays(-3),
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now.AddDays(-2)
        });

        await context.SaveChangesAsync();

        var sut = CreateSut(context, now);
        var first = await sut.RunPostServiceSurveyAsync();
        var second = await sut.RunPostServiceSurveyAsync();

        Assert.Equal(1, first.NotifiedCount);
        Assert.Equal(0, second.NotifiedCount);

        var surveyNotifications = await context.Notifications
            .Where(n => n.CustomerId == 102)
            .ToListAsync();
        Assert.Single(surveyNotifications);
        Assert.Equal("/jobs/survey/service-order/2001", surveyNotifications[0].LinkAction);
    }

    [Fact]
    public async Task RunInsuranceExpiryReminderAsync_RepeatedRun_DoesNotCreateDuplicateReminderNotifications()
    {
        var now = new DateTime(2026, 4, 8, 8, 0, 0, DateTimeKind.Utc);
        await using var context = CreateContext(nameof(RunInsuranceExpiryReminderAsync_RepeatedRun_DoesNotCreateDuplicateReminderNotifications));
        SeedCustomer(context, 103, "Customer Insurance");

        context.Vehicles.Add(new Vehicle
        {
            Id = 3001,
            LicensePlate = "51A-30001",
            ModelId = 1,
            IsDeleted = false
        });
        context.CustomerVehicles.Add(new CustomerVehicle
        {
            Id = 9301,
            CustomerId = 103,
            VehicleId = 3001,
            IsCurrentOwner = true,
            IsDeleted = false
        });
        context.VehicleInsurances.Add(new VehicleInsurance
        {
            Id = 4001,
            VehicleId = 3001,
            InsuranceCompanyId = 1,
            PolicyNumber = "PLCY-4001",
            ExpiryDate = DateOnly.FromDateTime(now.Date.AddDays(7))
        });

        await context.SaveChangesAsync();

        var sut = CreateSut(context, now);
        var first = await sut.RunInsuranceExpiryReminderAsync();
        var second = await sut.RunInsuranceExpiryReminderAsync();

        Assert.Equal(1, first.NotifiedCount);
        Assert.Equal(0, second.NotifiedCount);

        var reminders = await context.Notifications
            .Where(n => n.CustomerId == 103)
            .ToListAsync();
        Assert.Single(reminders);
        Assert.Contains("/jobs/insurance/4001/", reminders[0].LinkAction);
    }

    [Fact]
    public async Task RunChatbotSessionCleanupAsync_RepeatedRun_IsIdempotent()
    {
        var now = new DateTime(2026, 4, 8, 8, 0, 0, DateTimeKind.Utc);
        await using var context = CreateContext(nameof(RunChatbotSessionCleanupAsync_RepeatedRun_IsIdempotent));
        SeedCustomer(context, 104, "Customer Session");

        context.ChatbotSessions.Add(new ChatbotSession
        {
            Id = 5001,
            SessionToken = "cleanup-active",
            CustomerId = 104,
            Status = nameof(ChatbotSessionStatus.ACTIVE),
            LastActivityAt = now.AddDays(-2),
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now.AddDays(-2)
        });
        context.ChatbotSessions.Add(new ChatbotSession
        {
            Id = 5002,
            SessionToken = "cleanup-expired",
            CustomerId = 104,
            Status = nameof(ChatbotSessionStatus.EXPIRED),
            LastActivityAt = now.AddDays(-40),
            ClosedAt = now.AddDays(-35),
            CreatedAt = now.AddDays(-50),
            UpdatedAt = now.AddDays(-35)
        });

        await context.SaveChangesAsync();

        var sut = CreateSut(context, now);
        var first = await sut.RunChatbotSessionCleanupAsync();
        var second = await sut.RunChatbotSessionCleanupAsync();

        Assert.Equal(2, first.UpdatedCount);
        Assert.Equal(0, second.UpdatedCount);

        var activeToExpired = await context.ChatbotSessions.FirstAsync(x => x.Id == 5001);
        var expiredToArchived = await context.ChatbotSessions.FirstAsync(x => x.Id == 5002);

        Assert.Equal(nameof(ChatbotSessionStatus.EXPIRED), activeToExpired.Status);
        Assert.Equal(nameof(ChatbotSessionStatus.ARCHIVED), expiredToArchived.Status);
        Assert.Equal(2, await context.ChatbotActionLogs.CountAsync(x => x.ActionType == "SESSION_CLEANUP"));
    }

    private static GarageContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new GarageContext(options, new HttpContextAccessor());
    }

    private static CustomerAutomationJobProcessor CreateSut(GarageContext context, DateTime utcNow)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new CustomerAutomationJobsOptions());
        var clock = new FakeClock(utcNow);
        return new CustomerAutomationJobProcessor(
            context,
            options,
            clock,
            NullLogger<CustomerAutomationJobProcessor>.Instance);
    }

    private static void SeedCustomer(GarageContext context, long customerId, string name)
    {
        context.Customers.Add(new Customer
        {
            Id = customerId,
            FullName = name,
            Type = "CUSTOMER",
            IsActive = true,
            IsDeleted = false
        });
    }

    private sealed class FakeClock : ISystemClock
    {
        private readonly DateTime _utcNow;

        public FakeClock(DateTime utcNow)
        {
            _utcNow = utcNow;
        }

        public DateTime UtcNow => _utcNow;

        public DateTime LocalNow => _utcNow;

        public DateOnly LocalToday => DateOnly.FromDateTime(_utcNow);
    }
}
