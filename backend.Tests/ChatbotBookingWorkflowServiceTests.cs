using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class ChatbotBookingWorkflowServiceTests
{
    [Fact]
    public async Task RunAsync_NoSlots_ReturnsNoSlotsPrompt()
    {
        await using var ctx = CreateContext(nameof(RunAsync_NoSlots_ReturnsNoSlotsPrompt));
        SeedBaseData(ctx);
        var fake = new FakeAppointmentService
        {
            SlotProvider = (_, _, _, _, _, _) => Task.FromResult<IEnumerable<AvailableSlotResponseDto>>([])
        };
        var sut = CreateSut(ctx, fake);
        var state = BuildState(step: 3, vehicleId: 1001, serviceIds: [11], branchId: 501, date: DateOnly.FromDateTime(DateTime.UtcNow));

        var result = await sut.RunAsync(101, 1, 501, state, "today");

        Assert.Equal("COMPUTE_AVAILABLE_SLOTS", result.Step);
        Assert.Contains("khung", result.Prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_BranchClosed_ReturnsNoActiveBranchPrompt()
    {
        await using var ctx = CreateContext(nameof(RunAsync_BranchClosed_ReturnsNoActiveBranchPrompt));
        SeedBaseData(ctx, branchStatus: "CLOSED");
        var sut = CreateSut(ctx, new FakeAppointmentService());
        var state = BuildState(step: 2, vehicleId: 1001, serviceIds: [11]);

        var result = await sut.RunAsync(101, 1, null, state, "branch 501");

        Assert.Contains("ACTIVE", result.Prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_RaceCondition_ReturnsPickSlotAgain()
    {
        await using var ctx = CreateContext(nameof(RunAsync_RaceCondition_ReturnsPickSlotAgain));
        SeedBaseData(ctx);
        var slot = BuildSlot();
        var fake = new FakeAppointmentService
        {
            SlotProvider = (_, _, _, _, _, _) => Task.FromResult<IEnumerable<AvailableSlotResponseDto>>([slot]),
            ThrowOnBook = true
        };
        var sut = CreateSut(ctx, fake);
        var state = BuildState(step: 4, vehicleId: 1001, serviceIds: [11], branchId: 501, date: DateOnly.FromDateTime(DateTime.UtcNow), startUtc: slot.StartTime);

        var result = await sut.RunAsync(101, 1, 501, state, "yes");

        Assert.Equal("COMPUTE_AVAILABLE_SLOTS", result.Step);
        Assert.Contains("Vui", result.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.True(await ctx.ChatbotActionLogs.AnyAsync(x => x.ActionType == "CREATE_APPOINTMENT" && x.Status == nameof(ChatbotActionStatus.FAILED)));
    }

    [Fact]
    public async Task RunAsync_MultipleServices_BooksGroupSession()
    {
        await using var ctx = CreateContext(nameof(RunAsync_MultipleServices_BooksGroupSession));
        SeedBaseData(ctx);
        var slot = BuildSlot();
        var fake = new FakeAppointmentService
        {
            SlotProvider = (_, _, _, _, _, _) => Task.FromResult<IEnumerable<AvailableSlotResponseDto>>([slot])
        };
        var sut = CreateSut(ctx, fake);
        var state = BuildState(step: 4, vehicleId: 1001, serviceIds: [11, 12], branchId: 501, date: DateOnly.FromDateTime(DateTime.UtcNow), startUtc: slot.StartTime);

        var result = await sut.RunAsync(101, 1, 501, state, "yes");

        Assert.Equal("COMPLETED", result.Step);
        Assert.NotNull(result.BookingResult?.BookingGroupId);
        Assert.True(fake.BatchBooked);
    }

    [Fact]
    public async Task RunAsync_NoShowWarning_ReturnsWarningForHighNoShowCount()
    {
        await using var ctx = CreateContext(nameof(RunAsync_NoShowWarning_ReturnsWarningForHighNoShowCount));
        SeedBaseData(ctx, noShowCount: 3);
        var slot = BuildSlot();
        var fake = new FakeAppointmentService
        {
            SlotProvider = (_, _, _, _, _, _) => Task.FromResult<IEnumerable<AvailableSlotResponseDto>>([slot])
        };
        var sut = CreateSut(ctx, fake);
        var state = BuildState(step: 3, vehicleId: 1001, serviceIds: [11], branchId: 501, date: DateOnly.FromDateTime(DateTime.UtcNow));

        var result = await sut.RunAsync(101, 1, 501, state, "slot 1");

        Assert.NotNull(result.NoShowWarning);
        Assert.Contains("no-show", result.NoShowWarning!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_OneShotBookingRequest_AutoFillsButStillRequiresFinalConfirmation()
    {
        await using var ctx = CreateContext(nameof(RunAsync_OneShotBookingRequest_AutoFillsButStillRequiresFinalConfirmation));
        SeedBaseData(ctx);
        var fake = new FakeAppointmentService
        {
            SlotProvider = (_, date, _, _, _, _) =>
            {
                var start = date.ToDateTime(new TimeOnly(9, 0));
                return Task.FromResult<IEnumerable<AvailableSlotResponseDto>>(
                [
                    new AvailableSlotResponseDto
                    {
                        StartTime = start,
                        EndTime = start.AddHours(1),
                        IsAvailable = true,
                        RemainingCapacity = 2,
                        MaxCapacity = 2
                    }
                ]);
            }
        };
        var sut = CreateSut(ctx, fake);

        var result = await sut.RunAsync(
            101,
            1,
            501,
            string.Empty,
            "dat lich Bao duong cap 1 vao ngay mai luc 9h");

        Assert.Equal("FINALIZE_BOOKING", result.Step);
        Assert.Null(result.BookingResult);
        Assert.Contains("Vui", result.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.False(fake.Booked);
    }

    [Fact]
    public async Task RunAsync_GenericMaintenanceAfterVehicleChoice_AsksForDetailedService()
    {
        await using var ctx = CreateContext(nameof(RunAsync_GenericMaintenanceAfterVehicleChoice_AsksForDetailedService));
        SeedBaseData(ctx);
        SeedSecondVehicleAndBranch(ctx);
        SeedAmbiguousMaintenanceCatalog(ctx);
        var fake = new FakeAppointmentService();
        var sut = CreateSut(ctx, fake);

        var first = await sut.RunAsync(
            101,
            1,
            501,
            string.Empty,
            "dat lich bao duong vao ngay mai luc 9h");

        var second = await sut.RunAsync(
            101,
            1,
            501,
            first.UpdatedIntentState,
            "xe 1");

        Assert.Equal("SELECT_SERVICES", second.Step);
        Assert.True(second.RequiresUserInput);
        Assert.True(second.ServiceOptions.Count >= 2);
        Assert.Empty(second.BranchOptions);
        Assert.Null(second.BookingResult);
        Assert.False(fake.Booked);
        Assert.False(fake.BatchBooked);
    }

    [Fact]
    public async Task RunAsync_DetailedMaintenanceAfterVehicleChoice_AsksForBranchWhenMissingLocation()
    {
        await using var ctx = CreateContext(nameof(RunAsync_DetailedMaintenanceAfterVehicleChoice_AsksForBranchWhenMissingLocation));
        SeedBaseData(ctx);
        SeedSecondVehicleAndBranch(ctx);
        SeedAmbiguousMaintenanceCatalog(ctx);
        var fake = new FakeAppointmentService();
        var sut = CreateSut(ctx, fake);

        var first = await sut.RunAsync(
            101,
            1,
            501,
            string.Empty,
            "dat lich Bao duong cap 1 vao ngay mai luc 9h");

        Assert.Equal("IDENTIFY_OR_CREATE_VEHICLE", first.Step);
        Assert.True(first.RequiresUserInput);
        Assert.True(first.VehicleOptions.Count >= 2);

        var second = await sut.RunAsync(
            101,
            1,
            501,
            first.UpdatedIntentState,
            "xe 1");

        Assert.Equal("SELECT_BRANCH", second.Step);
        Assert.True(second.RequiresUserInput);
        Assert.True(second.BranchOptions.Count >= 2);
        Assert.Empty(second.SlotOptions);
        Assert.Null(second.BookingResult);
        Assert.False(fake.Booked);
        Assert.False(fake.BatchBooked);
    }

    [Fact]
    public async Task RunAsync_SelectingDetailedService_DoesNotLoopBackToAnotherServiceCategory()
    {
        await using var ctx = CreateContext(nameof(RunAsync_SelectingDetailedService_DoesNotLoopBackToAnotherServiceCategory));
        SeedBaseData(ctx);
        SeedSecondVehicleAndBranch(ctx);
        SeedQuickServiceCategory(ctx);
        var fake = new FakeAppointmentService();
        var sut = CreateSut(ctx, fake);
        var state = BuildState(
            step: 1,
            vehicleId: 1001,
            serviceCategoryId: 1,
            prefillInput: "dat lich bao duong vao ngay mai luc 9h",
            autoConfirmRequested: true);

        var result = await sut.RunAsync(
            101,
            1,
            501,
            state,
            "dich vu 2");

        Assert.Equal("SELECT_BRANCH", result.Step);
        Assert.True(result.RequiresUserInput);
        Assert.True(result.BranchOptions.Count >= 2);
        Assert.Empty(result.ServiceOptions);
        Assert.Null(result.BookingResult);
        Assert.False(fake.Booked);
        Assert.False(fake.BatchBooked);
    }

    private static ChatbotBookingWorkflowService CreateSut(GarageContext ctx, FakeAppointmentService fake)
        => new(
            ctx,
            fake,
            new FakeVoucherResolver(),
            new SystemClock(),
            NullLogger<ChatbotBookingWorkflowService>.Instance);

    private static string BuildState(
        int step,
        long? vehicleId = null,
        long? serviceCategoryId = null,
        IEnumerable<long>? serviceIds = null,
        long? branchId = null,
        DateOnly? date = null,
        DateTime? startUtc = null,
        string? prefillInput = null,
        bool autoConfirmRequested = false)
    {
        var payload = new
        {
            booking = new
            {
                Step = step,
                VehicleId = vehicleId,
                ServiceCategoryId = serviceCategoryId,
                ServiceIds = serviceIds?.ToArray() ?? [],
                BranchId = branchId,
                BookingDate = date?.ToString("yyyy-MM-dd"),
                SelectedSlotStartUtc = startUtc?.ToUniversalTime().ToString("O"),
                SelectedSlotEndUtc = startUtc?.ToUniversalTime().AddHours(1).ToString("O"),
                PrefillInput = prefillInput,
                AutoConfirmRequested = autoConfirmRequested
            }
        };
        return System.Text.Json.JsonSerializer.Serialize(payload);
    }

    private static AvailableSlotResponseDto BuildSlot()
    {
        var start = DateTime.UtcNow.Date.AddHours(9);
        return new AvailableSlotResponseDto
        {
            StartTime = start,
            EndTime = start.AddHours(1),
            IsAvailable = true,
            RemainingCapacity = 2,
            MaxCapacity = 2
        };
    }

    private static void SeedBaseData(GarageContext ctx, string branchStatus = "ACTIVE", int noShowCount = 0)
    {
        ctx.Customers.Add(new Customer
        {
            Id = 101,
            FullName = "Customer One",
            Phone = "0900000000",
            Type = "CUSTOMER",
            IsActive = true,
            IsDeleted = false,
            NoShowCount = noShowCount
        });
        ctx.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "BRANDA", Name = "BrandA", IsDeleted = false, IsActive = true });
        ctx.VehicleModels.Add(new VehicleModel { Id = 901, BrandId = 1, ModelName = "ModelX", IsDeleted = false });
        ctx.ServiceCategories.Add(new ServiceCategory { Id = 1, Code = "MAINT", Name = "Bao duong", IsActive = true, IsDeleted = false });
        ctx.Vehicles.Add(new Vehicle { Id = 1001, LicensePlate = "51A12345", ModelId = 901, IsDeleted = false });
        ctx.CustomerVehicles.Add(new CustomerVehicle { Id = 2001, CustomerId = 101, VehicleId = 1001, IsCurrentOwner = true, IsDeleted = false });
        ctx.Branches.Add(new Branch { Id = 501, Name = "Central", Status = branchStatus, IsDeleted = false, Hotline = "1900" });
        for (var day = 0; day <= 6; day++)
        {
            ctx.BranchOperatingHours.Add(new BranchOperatingHour
            {
                Id = 3000 + day,
                BranchId = 501,
                DayOfWeek = day,
                OpenTime = new TimeOnly(8, 0),
                CloseTime = new TimeOnly(18, 0),
                SlotDurationMinutes = 30,
                MaxConcurrentBookings = 2,
                IsDayOff = false
            });
        }
        ctx.StandardServices.Add(new StandardService { Id = 11, Code = "SVC11", Name = "Bao duong cap 1", CategoryId = 1, BasePrice = 100000, StandardMinutes = 45, IsActive = true, IsDeleted = false });
        ctx.StandardServices.Add(new StandardService { Id = 12, Code = "SVC12", Name = "Bao duong cap 2", CategoryId = 1, BasePrice = 150000, StandardMinutes = 30, IsActive = true, IsDeleted = false });
        ctx.SaveChanges();
    }

    private static void SeedSecondVehicleAndBranch(GarageContext ctx)
    {
        ctx.Vehicles.Add(new Vehicle { Id = 1002, LicensePlate = "51B67890", ModelId = 901, IsDeleted = false });
        ctx.CustomerVehicles.Add(new CustomerVehicle { Id = 2002, CustomerId = 101, VehicleId = 1002, IsCurrentOwner = true, IsDeleted = false });
        ctx.Branches.Add(new Branch { Id = 502, Name = "East Branch", Status = "ACTIVE", IsDeleted = false, Hotline = "1901" });
        for (var day = 0; day <= 6; day++)
        {
            ctx.BranchOperatingHours.Add(new BranchOperatingHour
            {
                Id = 4000 + day,
                BranchId = 502,
                DayOfWeek = day,
                OpenTime = new TimeOnly(8, 0),
                CloseTime = new TimeOnly(18, 0),
                SlotDurationMinutes = 30,
                MaxConcurrentBookings = 2,
                IsDayOff = false
            });
        }
        ctx.SaveChanges();
    }

    private static void SeedAmbiguousMaintenanceCatalog(GarageContext ctx)
    {
        var category = ctx.ServiceCategories.Single(x => x.Id == 1);
        category.Name = "Bao duong";
        var serviceOne = ctx.StandardServices.Single(x => x.Id == 11);
        serviceOne.Name = "Bao duong cap 1";
        var serviceTwo = ctx.StandardServices.Single(x => x.Id == 12);
        serviceTwo.Name = "Bao duong cap 2";
        ctx.SaveChanges();
    }

    private static void SeedQuickServiceCategory(GarageContext ctx)
    {
        ctx.ServiceCategories.Add(new ServiceCategory
        {
            Id = 2,
            Code = "QUICK",
            Name = "Dich vu nhanh",
            IsActive = true,
            IsDeleted = false
        });
        ctx.StandardServices.Add(new StandardService
        {
            Id = 54,
            Code = "DGMINI",
            Name = "Goi kiem tra nhanh",
            CategoryId = 2,
            BasePrice = 0,
            StandardMinutes = 15,
            IsActive = true,
            IsDeleted = false
        });
        ctx.StandardServices.Add(new StandardService
        {
            Id = 66,
            Code = "EVACT",
            Name = "Bao duong co cau khoa cong sac",
            CategoryId = 2,
            BasePrice = 300000,
            StandardMinutes = 30,
            IsActive = true,
            IsDeleted = false
        });
        ctx.SaveChanges();
    }
    private static GarageContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new GarageContext(options, new HttpContextAccessor());
    }

    private sealed class FakeAppointmentService : IAppointmentService
    {
        public Func<long, DateOnly, string?, string?, int, bool, Task<IEnumerable<AvailableSlotResponseDto>>>? SlotProvider { get; set; }
        public bool ThrowOnBook { get; set; }
        public bool BatchBooked { get; private set; }
        public bool Booked { get; private set; }

        public Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(long branchId, DateOnly date, string? requestedServiceIds = null, string? bookingFlow = null, int vehicleCount = 1, bool isParallel = true)
            => SlotProvider?.Invoke(branchId, date, requestedServiceIds, bookingFlow, vehicleCount, isParallel)
               ?? Task.FromResult<IEnumerable<AvailableSlotResponseDto>>([]);

        public Task<Appointment> BookAsync(long customerId, BookAppointmentRequestDto dto)
        {
            if (ThrowOnBook) throw new InvalidOperationException("capacity conflict");
            Booked = true;
            return Task.FromResult(new Appointment { Id = 8881 });
        }

        public Task<BookingGroupResponseDto> BookBatchForCustomerAsync(long actorUserId, long targetCustomerId, BookAppointmentBatchRequestDto dto)
        {
            BatchBooked = true;
            return Task.FromResult(new BookingGroupResponseDto { Id = 777, GroupCode = "BG-777" });
        }

        public Task<BookingGroupResponseDto> BookBatchAsync(long customerId, BookAppointmentBatchRequestDto dto) => throw new NotImplementedException();
        public Task<IEnumerable<AppointmentDetailResponseDto>> GetAllDetailedAsync(string? status = null) => throw new NotImplementedException();
        public Task<SlotHoldResponseDto> CreateSlotHoldAsync(SlotHoldRequestDto dto)
            => Task.FromResult(new SlotHoldResponseDto
            {
                HoldToken = "HOLD_TEST",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                HoldMinutes = 5
            });
        public Task<bool> ReleaseSlotHoldAsync(string holdToken) => throw new NotImplementedException();
        public Task<Appointment?> ConfirmAsync(long id, long? actorUserId = null) => throw new NotImplementedException();
        public Task<Appointment?> CancelAsync(long id, string? reason, long? actorUserId = null) => throw new NotImplementedException();
        public Task<Appointment?> CheckInAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null) => throw new NotImplementedException();
        public Task<Appointment?> ArriveAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null) => throw new NotImplementedException();
        public Task<WorkSlotDynamicExtensionResponseDto> ProcessDynamicExtensionAsync(long workSlotId, WorkSlotDynamicExtensionRequestDto dto, long? actorUserId = null) => throw new NotImplementedException();
        public Task<int> AutoMarkNoShowsAsync(long branchId, DateTime? now = null) => throw new NotImplementedException();
        public Task<BranchBookingHealthResponseDto> GetBranchBookingHealthAsync(long branchId, DateTime? at = null) => throw new NotImplementedException();
        public Task<DispatchDashboardResponseDto> GetDispatchDashboardAsync(long branchId, DateTime? at = null) => throw new NotImplementedException();
        public Task<DispatchCycleResponseDto> RunDispatchCycleAsync(long branchId, DateTime? at = null) => throw new NotImplementedException();
        public Task<OvernightLockResponseDto> LockOvernightSlotsAsync(long branchId, DateOnly? businessDate = null) => throw new NotImplementedException();
        public Task<IEnumerable<AppointmentDetailResponseDto>> GetCustomerAppointmentsAsync(long customerId) => throw new NotImplementedException();
        public Task<IEnumerable<AppointmentDetailResponseDto>> GetBranchAppointmentsAsync(long branchId, string? status = null) => throw new NotImplementedException();
        public Task<AppointmentDetailResponseDto?> GetDetailedAsync(long id) => throw new NotImplementedException();
        public Task<IEnumerable<Appointment>> GetAllAsync() => throw new NotImplementedException();
        public Task<Appointment?> GetByIdAsync(object id) => throw new NotImplementedException();
        public Task<Appointment> AddAsync(Appointment entity) => throw new NotImplementedException();
        public Task<Appointment?> UpdateAsync(object id, Appointment entity) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(object id) => throw new NotImplementedException();
    }

    private sealed class FakeVoucherResolver : IChatbotVoucherResolverService
    {
        public Task<ChatbotVoucherResolveResult> ResolveBestVoucherAsync(
            ChatbotVoucherResolveRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatbotVoucherResolveResult
            {
                IsApplied = false,
                ReasonCode = "NO_VALID_VOUCHER",
                Message = "No voucher in test setup."
            });
        }
    }
}


