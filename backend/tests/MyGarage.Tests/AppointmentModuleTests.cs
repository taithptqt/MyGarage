using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using MyGarage.Domain.Appointments;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class AppointmentModuleTests
{
    [Theory]
    [InlineData("PENDING", true)]
    [InlineData("LATE", true)]
    [InlineData("CONFIRMED", false)]
    [InlineData("ARRIVED", false)]
    [InlineData("CANCELED", false)]
    public void AppointmentLifecycleRules_CanConfirm_ValidatesCorrectly(string currentStatus, bool expected)
    {
        var result = AppointmentLifecycleRules.CanConfirm(currentStatus);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("PENDING", true)]
    [InlineData("CONFIRMED", true)]
    [InlineData("ARRIVED", true)]
    [InlineData("CANCELED", false)]
    [InlineData("NO_SHOW", false)]
    [InlineData("COMPLETED", false)]
    public void AppointmentLifecycleRules_CanCancel_ValidatesCorrectly(string currentStatus, bool expected)
    {
        var result = AppointmentLifecycleRules.CanCancel(currentStatus);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("CONFIRMED", true)]
    [InlineData("LATE", true)]
    [InlineData("PENDING", false)]
    [InlineData("ARRIVED", false)]
    public void AppointmentLifecycleRules_CanCheckIn_ValidatesCorrectly(string currentStatus, bool expected)
    {
        var result = AppointmentLifecycleRules.CanCheckIn(currentStatus);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AppointmentLifecycleRules_BuildCancelReason_AddsLateCancelTagWithinThreshold()
    {
        var now = new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);
        var lateStartTime = now.AddMinutes(30); // 30 mins away < 60 mins -> Late cancel
        var futureStartTime = now.AddHours(5);  // 5 hours away -> Normal cancel

        var lateReason = AppointmentLifecycleRules.BuildCancelReason(lateStartTime, "Busy schedule", now);
        Assert.Equal("[LATE_CANCEL] Busy schedule", lateReason);

        var normalReason = AppointmentLifecycleRules.BuildCancelReason(futureStartTime, "Busy schedule", now);
        Assert.Equal("Busy schedule", normalReason);
    }

    [Fact]
    public async Task Customer_CannotCancel_AnotherCustomersAppointment()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Customers.Add(new Customer { Id = 101, FullName = "Customer 101", Phone = "0901111111", Type = "INDIVIDUAL", SecurityStamp = "test-stamp" });
            db.Customers.Add(new Customer { Id = 102, FullName = "Customer 102", Phone = "0902222222", Type = "INDIVIDUAL", SecurityStamp = "test-stamp" });
            db.Appointments.Add(new Appointment
            {
                Id = 8101,
                BranchId = 1,
                CustomerId = 102, // Owned by Customer 102
                Status = "PENDING",
                StartTime = DateTime.UtcNow.AddDays(1),
                EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
                CreatedAt = DateTime.UtcNow
            });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        // Logged in as Customer 101
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(101, "Customer"));

        var response = await client.PatchAsync("/api/appointments/8101", JsonContent.Create(new
        {
            status = "CANCELED",
            reason = "Need to cancel"
        }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotTransitionAppointment_ToStaffControlledStatuses()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Customers.Add(new Customer { Id = 103, FullName = "Customer 103", Phone = "0903333333", Type = "INDIVIDUAL", SecurityStamp = "test-stamp" });
            db.Appointments.Add(new Appointment
            {
                Id = 8102,
                BranchId = 1,
                CustomerId = 103,
                Status = "PENDING",
                StartTime = DateTime.UtcNow.AddDays(1),
                EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
                CreatedAt = DateTime.UtcNow
            });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(103, "Customer"));

        // Customer attempts to self-confirm
        var response = await client.PatchAsync("/api/appointments/8102", JsonContent.Create(new
        {
            status = "CONFIRMED"
        }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffFromOtherBranch_CannotUpdate_AppointmentOfDifferentBranch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Staffs.Add(new Staff
            {
                Id = 202,
                FullName = "Nhân viên CN2",
                Phone = "0905556677",
                Email = "staff2@example.com",
                PasswordHash = "hash",
                BranchId = 2,
                Status = "ACTIVE",
                SecurityStamp = "test-stamp"
            });
            db.Customers.Add(new Customer { Id = 104, FullName = "Customer 104", Phone = "0904444444", Type = "INDIVIDUAL", SecurityStamp = "test-stamp" });
            db.Appointments.Add(new Appointment
            {
                Id = 8103,
                BranchId = 1, // Branch 1
                CustomerId = 104,
                Status = "PENDING",
                StartTime = DateTime.UtcNow.AddDays(1),
                EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
                CreatedAt = DateTime.UtcNow
            });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        // Staff belongs to Branch 2
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(202, "Staff", "RECEPTIONIST", 2));

        var response = await client.PatchAsync("/api/appointments/8103", JsonContent.Create(new
        {
            status = "CONFIRMED"
        }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NonStaff_CannotCall_SaveReceptionBooking()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Customers.Add(new Customer { Id = 105, FullName = "Customer 105", Phone = "0905555555", Type = "INDIVIDUAL", SecurityStamp = "test-stamp" });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(105, "Customer"));

        var response = await client.PostAsync("/api/appointments/reception-bookings", JsonContent.Create(new
        {
            branchId = 1,
            customerPhone = "0905555555"
        }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffFromOtherBranch_CannotCreateReceptionBooking_ForDifferentBranch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Staffs.Add(new Staff
            {
                Id = 202,
                FullName = "Nhân viên CN2",
                Phone = "0905556677",
                Email = "staff2@example.com",
                PasswordHash = "hash",
                BranchId = 2,
                Status = "ACTIVE",
                SecurityStamp = "test-stamp"
            });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        // Staff at Branch 2
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(202, "Staff", "RECEPTIONIST", 2));

        // Attempt booking for Branch 1
        var response = await client.PostAsync("/api/appointments/reception-bookings", JsonContent.Create(new
        {
            branchId = 1,
            customerPhone = "0909999999"
        }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffFromOtherBranch_CannotDecideEditRequest_ForDifferentBranch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            if (!db.Roles.Any(r => r.Id == 2))
            {
                db.Roles.Add(new Role { Id = 2, RoleCode = "MANAGER", RoleName = "Quản lý", Scope = "STAFF" });
            }
            db.Staffs.Add(new Staff
            {
                Id = 202,
                FullName = "Nhân viên CN2",
                Phone = "0905556677",
                Email = "staff2@example.com",
                PasswordHash = "hash",
                BranchId = 2,
                RoleId = 2,
                Status = "ACTIVE",
                SecurityStamp = "test-stamp"
            });
            db.Customers.Add(new Customer { Id = 106, FullName = "Customer 106", Phone = "0906666666", Type = "INDIVIDUAL", SecurityStamp = "test-stamp" });
            db.Appointments.Add(new Appointment
            {
                Id = 8106,
                BranchId = 1, // Branch 1
                CustomerId = 106,
                Status = "PENDING",
                StartTime = DateTime.UtcNow.AddDays(1),
                EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
                CreatedAt = DateTime.UtcNow
            });
            db.AuditLogs.Add(new AuditLog
            {
                Id = 9901,
                UserId = 200,
                UserType = "STAFF",
                Action = "REQUEST_EDIT",
                TableName = "APPOINTMENTS",
                RecordId = 8106,
                BranchId = 1, // Branch 1
                NewValue = JsonSerializer.Serialize(new { Status = "PENDING", Section = "APPOINTMENT" }),
                Timestamp = DateTime.UtcNow
            });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        // Staff at Branch 2
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(202, "Staff", "MANAGER", 2));

        var response = await client.PatchAsync("/api/appointments/8106/edit-requests/9901", JsonContent.Create(new
        {
            decision = "APPROVE"
        }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
