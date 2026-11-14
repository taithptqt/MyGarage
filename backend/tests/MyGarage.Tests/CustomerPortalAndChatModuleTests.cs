using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyGarage.Application.Authorization;
using MyGarage.Application.CustomerPortal.DTOs;
using MyGarage.Application.Notifications.DTOs;
using MyGarage.Common;
using MyGarage.Data;
using MyGarage.Domain.Rules;
using MyGarage.Models;
using MyGarage.Services;
using MyGarage.Support;
using Xunit;

namespace MyGarage.Tests;

public class CustomerPortalAndChatModuleTests
{
    // =========================================================================
    // SECTION 1: CustomerPortalDomainRules Unit Tests
    // =========================================================================

    [Theory]
    [InlineData("NEW", false, false, CustomerPortalDomainRules.StageReceived)]
    [InlineData("WAITING_RECEIVE", false, false, CustomerPortalDomainRules.StageReceived)]
    [InlineData("RECEIVED", false, false, CustomerPortalDomainRules.StageReceived)]
    [InlineData("CHECKED_IN", false, false, CustomerPortalDomainRules.StageReceived)]
    [InlineData("WAITING", false, false, CustomerPortalDomainRules.StageReceived)]
    [InlineData("DIAGNOSING", false, false, CustomerPortalDomainRules.StageInspecting)]
    [InlineData("WAITING_PARTS", false, false, CustomerPortalDomainRules.StageWaitingParts)]
    [InlineData("QUOTING", false, false, CustomerPortalDomainRules.StageQuote)]
    [InlineData("WAITING_APPROVAL", false, false, CustomerPortalDomainRules.StageQuote)]
    [InlineData("APPROVED", false, false, CustomerPortalDomainRules.StageQuote)]
    [InlineData("IN_PROGRESS", false, false, CustomerPortalDomainRules.StageRepairing)]
    [InlineData("REPAIRING", false, false, CustomerPortalDomainRules.StageRepairing)]
    [InlineData("READY", false, false, CustomerPortalDomainRules.StageReady)]
    [InlineData("WAITING_PAYMENT", false, false, CustomerPortalDomainRules.StageReady)]
    [InlineData("COMPLETED", false, false, CustomerPortalDomainRules.StageReady)]
    [InlineData("CLOSED", false, false, CustomerPortalDomainRules.StageDelivered)]
    [InlineData("DELIVERED", false, false, CustomerPortalDomainRules.StageDelivered)]
    [InlineData("CANCELED", false, false, CustomerPortalDomainRules.StageCanceled)]
    [InlineData("UNKNOWN_STATUS", false, false, CustomerPortalDomainRules.StageReceived)]
    public void StageMapper_ResolvesExpectedStage(string status, bool hasActiveDiag, bool hasWaitingParts, string expectedStage)
    {
        var resolved = CustomerPortalDomainRules.ResolveStage(status, hasActiveDiag, hasWaitingParts);
        Assert.Equal(expectedStage, resolved);
    }

    [Fact]
    public void StageMapper_ActiveDiagnosisAndWaitingPartsLine_Precedence()
    {
        // Preserve the legacy precedence while migrating the mapper; this is not
        // evidence that every legacy precedence rule is a confirmed business rule.
        foreach (var status in new[] { "WAITING", "APPROVED", "IN_PROGRESS", "CANCELED" })
        foreach (var diagnosis in new[] { false, true })
        foreach (var waitingParts in new[] { false, true })
            Assert.Equal(CustomerPortalStageMapper.ResolveStage(status, diagnosis, waitingParts),
                CustomerPortalDomainRules.ResolveStage(status, diagnosis, waitingParts));
    }

    [Theory]
    [InlineData(CustomerPortalDomainRules.StageReceived, "Đã tiếp nhận")]
    [InlineData(CustomerPortalDomainRules.StageInspecting, "Đang kiểm tra")]
    [InlineData(CustomerPortalDomainRules.StageQuote, "Chờ báo giá / chờ duyệt")]
    [InlineData(CustomerPortalDomainRules.StageRepairing, "Đang sửa")]
    [InlineData(CustomerPortalDomainRules.StageWaitingParts, "Chờ phụ tùng")]
    [InlineData(CustomerPortalDomainRules.StageReady, "Sẵn sàng")]
    [InlineData(CustomerPortalDomainRules.StageDelivered, "Đã giao")]
    [InlineData(CustomerPortalDomainRules.StageCanceled, "Đã hủy")]
    [InlineData("UNKNOWN", "Đang xử lý")]
    public void StageMapper_VietnameseLabels_MatchExpectedText(string code, string expectedLabel)
    {
        var label = CustomerPortalDomainRules.ToVietnameseLabel(code);
        Assert.Equal(expectedLabel, label);
    }

    [Fact]
    public void StageMapper_BuildTimeline_GeneratesOrderedProgression()
    {
        var timeline = CustomerPortalDomainRules.BuildTimeline(CustomerPortalDomainRules.StageRepairing, canceled: false);
        Assert.Equal(6, timeline.Count);

        // Steps before REPAIRING are past
        Assert.Equal("past", timeline[0].State); // RECEIVED
        Assert.Equal("past", timeline[1].State); // INSPECTING
        Assert.Equal("past", timeline[2].State); // QUOTE
        // REPAIRING is current
        Assert.Equal("current", timeline[3].State);
        Assert.Equal(CustomerPortalDomainRules.StageRepairing, timeline[3].Code);
        // Steps after REPAIRING are upcoming
        Assert.Equal("upcoming", timeline[4].State); // READY
        Assert.Equal("upcoming", timeline[5].State); // DELIVERED
    }

    [Fact]
    public void StageMapper_BuildTimeline_HandlesWaitingPartsAndCanceled()
    {
        var wpTimeline = CustomerPortalDomainRules.BuildTimeline(CustomerPortalDomainRules.StageWaitingParts, canceled: false);
        Assert.Equal(6, wpTimeline.Count);
        Assert.Equal("current", wpTimeline[3].State);
        Assert.Equal("Chờ phụ tùng", wpTimeline[3].Label);

        var canceledTimeline = CustomerPortalDomainRules.BuildTimeline(CustomerPortalDomainRules.StageReceived, canceled: true);
        Assert.Single(canceledTimeline);
        Assert.Equal(CustomerPortalDomainRules.StageCanceled, canceledTimeline[0].Code);
        Assert.Equal("Đã hủy", canceledTimeline[0].Label);
        Assert.Equal("current", canceledTimeline[0].State);
    }

    [Fact]
    public void CustomerVehicleValidation_ValidatesPlateAndVin()
    {
        var (ok1, _) = CustomerPortalDomainRules.ValidateCustomerVehicleInput("51A-12345", "1HGCR2F83HA000000", "Đỏ", 2020);
        Assert.True(ok1);

        var (ok2, err2) = CustomerPortalDomainRules.ValidateCustomerVehicleInput("", null, null, null);
        Assert.False(ok2);
        Assert.Contains("không được để trống", err2);

        var (ok3, err3) = CustomerPortalDomainRules.ValidateCustomerVehicleInput("INVALID_PLATE_XYZ", null, null, null);
        Assert.False(ok3);
        Assert.Contains("định dạng chuẩn", err3);

        var (ok4, err4) = CustomerPortalDomainRules.ValidateCustomerVehicleInput("51A-12345", "INVALID_VIN_SHORT", null, null);
        Assert.False(ok4);
        Assert.Contains("Số VIN", err4);
    }

    [Fact]
    public void ChatMessageValidation_EnforcesInvariants()
    {
        // 1. Empty message fails
        var (v1, _) = CustomerPortalDomainRules.ValidateChatMessage("", null, hasOrderCard: false);
        Assert.False(v1);

        // 2. Normal text message passes
        var (v2, _) = CustomerPortalDomainRules.ValidateChatMessage("Xin chào gara", null, hasOrderCard: false);
        Assert.True(v2);

        // 3. Order card message without attachments passes
        var (v3, _) = CustomerPortalDomainRules.ValidateChatMessage(null, null, hasOrderCard: true);
        Assert.True(v3);

        // 4. Order card message WITH attachments fails (mutual exclusivity)
        var (v4, err4) = CustomerPortalDomainRules.ValidateChatMessage(null, new[] { "https://blob/pic.jpg" }, hasOrderCard: true);
        Assert.False(v4);
        Assert.Contains("không được kèm tệp", err4);

        // 5. Message exceeding 4000 chars fails
        var longText = new string('x', 4001);
        var (v5, err5) = CustomerPortalDomainRules.ValidateChatMessage(longText, null, hasOrderCard: false);
        Assert.False(v5);
        Assert.Contains("4000 ký tự", err5);

        // 6. Message with more than 10 attachments fails
        var excessiveAttachments = Enumerable.Range(1, 11).Select(i => $"https://blob/{i}.png").ToList();
        var (v6, err6) = CustomerPortalDomainRules.ValidateChatMessage(null, excessiveAttachments, hasOrderCard: false);
        Assert.False(v6);
        Assert.Contains("10 tệp", err6);
    }

    [Fact]
    public void AttachmentRules_WhitelistsExtensionsAndContentTypes()
    {
        Assert.True(CustomerPortalDomainRules.IsValidAttachmentExtension(".jpg"));
        Assert.True(CustomerPortalDomainRules.IsValidAttachmentExtension("png"));
        Assert.True(CustomerPortalDomainRules.IsValidAttachmentExtension(".pdf"));
        Assert.True(CustomerPortalDomainRules.IsValidAttachmentExtension(".docx"));
        Assert.True(CustomerPortalDomainRules.IsValidAttachmentExtension(".xlsx"));
        Assert.True(CustomerPortalDomainRules.IsValidAttachmentExtension(".zip"));

        Assert.False(CustomerPortalDomainRules.IsValidAttachmentExtension(".exe"));
        Assert.False(CustomerPortalDomainRules.IsValidAttachmentExtension(".sh"));
        Assert.False(CustomerPortalDomainRules.IsValidAttachmentExtension(".bat"));
        Assert.False(CustomerPortalDomainRules.IsValidAttachmentExtension(null));

        Assert.True(CustomerPortalDomainRules.IsValidAttachmentContentType("image/jpeg"));
        Assert.True(CustomerPortalDomainRules.IsValidAttachmentContentType("application/pdf"));
        Assert.False(CustomerPortalDomainRules.IsValidAttachmentContentType("application/x-msdownload"));

        Assert.True(CustomerPortalDomainRules.IsValidAttachmentFileSize(1024));
        Assert.False(CustomerPortalDomainRules.IsValidAttachmentFileSize(0));
        Assert.False(CustomerPortalDomainRules.IsValidAttachmentFileSize(15 * 1024 * 1024)); // > 10MB
    }

    [Fact]
    public void ConversationTransitions_EnforcesStateRules()
    {
        // Customer reopening a closed conversation
        Assert.True(CustomerPortalDomainRules.CanTransitionConversationStatus(
            CustomerPortalDomainRules.ConversationStatusClosed,
            CustomerPortalDomainRules.ConversationStatusWaitingForStaff,
            isStaff: false
        ));

        // Customer cannot close conversation directly
        Assert.False(CustomerPortalDomainRules.CanTransitionConversationStatus(
            CustomerPortalDomainRules.ConversationStatusOpen,
            CustomerPortalDomainRules.ConversationStatusClosed,
            isStaff: false
        ));

        // Staff can close conversation
        Assert.True(CustomerPortalDomainRules.CanTransitionConversationStatus(
            CustomerPortalDomainRules.ConversationStatusInProgress,
            CustomerPortalDomainRules.ConversationStatusClosed,
            isStaff: true
        ));

        // Unknown status is rejected
        Assert.False(CustomerPortalDomainRules.CanTransitionConversationStatus(
            CustomerPortalDomainRules.ConversationStatusOpen,
            "INVALID_STATUS",
            isStaff: true
        ));
    }

    // =========================================================================
    // SECTION 2: NotificationDomainRules Unit Tests
    // =========================================================================

    [Fact]
    public void NotificationReadState_EnforcesMonotonicity()
    {
        // unread (false) -> read (true) is valid
        Assert.True(NotificationDomainRules.CanTransitionNotificationReadState(false, true));

        // null -> read (true) is valid
        Assert.True(NotificationDomainRules.CanTransitionNotificationReadState(null, true));

        // read (true) -> read (true) is idempotent valid
        Assert.True(NotificationDomainRules.CanTransitionNotificationReadState(true, true));

        // read (true) -> unread (false) is FORBIDDEN (monotonic violation)
        Assert.False(NotificationDomainRules.CanTransitionNotificationReadState(true, false));
    }

    [Fact]
    public void LinkActionSanitization_AllowsApprovedInternalPrefixesOnly()
    {
        // Approved relative links
        Assert.True(NotificationDomainRules.SanitizeAndValidateLinkAction("/my-garage-dashboard", out var s1));
        Assert.Equal("/my-garage-dashboard", s1);

        Assert.True(NotificationDomainRules.SanitizeAndValidateLinkAction("/my-vehicles/12", out var s2));
        Assert.Equal("/my-vehicles/12", s2);

        Assert.True(NotificationDomainRules.SanitizeAndValidateLinkAction("/my-notifications", out var s3));
        Assert.Equal("/my-notifications", s3);

        Assert.True(NotificationDomainRules.SanitizeAndValidateLinkAction("/my-appointments?status=active", out var s4));
        Assert.Equal("/my-appointments?status=active", s4);

        Assert.True(NotificationDomainRules.SanitizeAndValidateLinkAction("/public/quotations/SO-10023", out var s5));
        Assert.Equal("/public/quotations/SO-10023", s5);

        // Optional link (null/empty) is valid
        Assert.True(NotificationDomainRules.SanitizeAndValidateLinkAction(null, out var sNull));
        Assert.Null(sNull);

        // Malicious or external links are REJECTED
        Assert.False(NotificationDomainRules.SanitizeAndValidateLinkAction("https://evil.com/phishing", out _));
        Assert.False(NotificationDomainRules.SanitizeAndValidateLinkAction("//evil.com/redirect", out _));
        Assert.False(NotificationDomainRules.SanitizeAndValidateLinkAction("/javascript:alert(1)", out _));
        Assert.False(NotificationDomainRules.SanitizeAndValidateLinkAction("/admin/database-dump", out _));
        Assert.False(NotificationDomainRules.SanitizeAndValidateLinkAction("ftp://files/download", out _));
    }

    [Fact]
    public void NotificationDeduplicationKeys_GenerateDeterministicTokens()
    {
        var key1 = NotificationDomainRules.BuildMaintenanceNotificationKey(101, 202, "6_MONTH");
        var key2 = NotificationDomainRules.BuildMaintenanceNotificationKey(101, 202, "6_month");
        Assert.Equal("NOTIF:MAINT:101:202:6_MONTH", key1);
        Assert.Equal(key1, key2);

        var apptKey = NotificationDomainRules.BuildAppointmentReminderNotificationKey(101, 505, "24h");
        Assert.Equal("NOTIF:APPT:101:505:24H", apptKey);

        Assert.Throws<ArgumentOutOfRangeException>(() => NotificationDomainRules.BuildMaintenanceNotificationKey(0, 202, "TOKEN"));
        Assert.Throws<ArgumentOutOfRangeException>(() => NotificationDomainRules.BuildMaintenanceNotificationKey(101, 0, "TOKEN"));
    }

    [Fact]
    public void MaintenanceReminderLifecycle_EnforcesTransitionsAndDueStatus()
    {
        // SCHEDULED -> SENT
        Assert.True(NotificationDomainRules.CanTransitionReminderStatus(
            NotificationDomainRules.StatusScheduled,
            NotificationDomainRules.StatusSent
        ));

        // SCHEDULED -> CANCELLED
        Assert.True(NotificationDomainRules.CanTransitionReminderStatus(
            NotificationDomainRules.StatusScheduled,
            NotificationDomainRules.StatusCancelled
        ));

        // SENT -> CONFIRMED
        Assert.True(NotificationDomainRules.CanTransitionReminderStatus(
            NotificationDomainRules.StatusSent,
            NotificationDomainRules.StatusConfirmed
        ));

        // CONFIRMED cannot transition back to SCHEDULED
        Assert.False(NotificationDomainRules.CanTransitionReminderStatus(
            NotificationDomainRules.StatusConfirmed,
            NotificationDomainRules.StatusScheduled
        ));

        // Due check
        var now = DateTime.UtcNow;
        Assert.True(NotificationDomainRules.IsReminderDue(now.AddHours(-1), NotificationDomainRules.StatusScheduled, now));
        Assert.False(NotificationDomainRules.IsReminderDue(now.AddHours(2), NotificationDomainRules.StatusScheduled, now));
        Assert.False(NotificationDomainRules.IsReminderDue(now.AddHours(-1), NotificationDomainRules.StatusSent, now));

        // Channels
        Assert.True(NotificationDomainRules.IsValidReminderChannel("EMAIL"));
        Assert.True(NotificationDomainRules.IsValidReminderChannel("IN_APP"));
        Assert.True(NotificationDomainRules.IsValidReminderChannel("sms"));
        Assert.True(NotificationDomainRules.IsValidReminderChannel("ZALO"));
        Assert.False(NotificationDomainRules.IsValidReminderChannel("TELEGRAM"));
    }

    // =========================================================================
    // SECTION 3: Authorization Port Tests
    // =========================================================================

    [Fact]
    public async Task CustomerPortalAuthorization_EnforcesCustomerIsolation()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PortalAuthDb_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Customers.Add(new Customer { Id = 100, FullName = "Khách Hàng 100", Type = "INDIVIDUAL", Note = CustomerDomainRules.SetPreferredBranchTag(null, 1) });
        db.Customers.Add(new Customer { Id = 200, FullName = "Khách Hàng 200", Type = "INDIVIDUAL" });
        db.Vehicles.Add(new Vehicle { Id = 10, LicensePlate = "51A-11111" });
        db.CustomerVehicles.Add(new CustomerVehicle { CustomerId = 100, VehicleId = 10, IsCurrentOwner = true, IsDeleted = false });
        db.Conversations.Add(new Conversation { Id = 1, CustomerId = 100, BranchId = 1 });
        db.Notifications.Add(new Notification { Id = 50, CustomerId = 100, Title = "Thông báo 1", Content = "Nội dung" });
        await db.SaveChangesAsync();

        var auth = new CustomerPortalAuthorizationService(db);

        var cust100Actor = new UserActorContext(100, "Customer", "CUSTOMER", null, IsAdmin: false);
        var cust200Actor = new UserActorContext(200, "Customer", "CUSTOMER", null, IsAdmin: false);
        var adminActor = new UserActorContext(999, "Admin", "ADMIN", null, IsAdmin: true);
        var staffBranch1 = new UserActorContext(300, "Staff", "STAFF", BranchId: 1, IsAdmin: false);
        var staffBranch2 = new UserActorContext(301, "Staff", "STAFF", BranchId: 2, IsAdmin: false);
        var staffNoBranch = new UserActorContext(302, "Staff", "STAFF", BranchId: null, IsAdmin: false);

        // Dashboard isolation
        Assert.True(await auth.CanAccessCustomerDashboardAsync(100, cust100Actor));
        Assert.False(await auth.CanAccessCustomerDashboardAsync(100, cust200Actor)); // Rejected!
        Assert.True(await auth.CanAccessCustomerDashboardAsync(100, adminActor));

        // Vehicle management isolation
        Assert.True(await auth.CanManageCustomerVehicleAsync(100, 10, cust100Actor));
        Assert.False(await auth.CanManageCustomerVehicleAsync(200, 10, cust200Actor));
        Assert.True(await auth.CanManageCustomerVehicleAsync(100, 10, staffBranch1));
        Assert.False(await auth.CanManageCustomerVehicleAsync(100, 10, staffBranch2));
        Assert.False(await auth.CanAccessCustomerDashboardAsync(100, staffNoBranch));
        Assert.False(await auth.CanManageCustomerVehicleAsync(100, 10, staffNoBranch));
        Assert.False(await auth.CanManageCustomerVehicleAsync(200, 10, staffBranch1));
        Assert.True(await auth.CanManageCustomerVehicleAsync(100, 10, adminActor));
        Assert.False(await auth.CanManageCustomerVehicleAsync(200, 10, adminActor));

        // Conversation isolation
        Assert.True(await auth.CanAccessConversationAsync(1, cust100Actor));
        Assert.False(await auth.CanAccessConversationAsync(1, cust200Actor));
        Assert.True(await auth.CanAccessConversationAsync(1, staffBranch1));

        // Branch inbox access: Staff yes, Customer no
        Assert.True(await auth.CanAccessBranchChatInboxAsync(1, staffBranch1));
        Assert.False(await auth.CanAccessBranchChatInboxAsync(1, cust100Actor));

        // Notification isolation
        Assert.True(await auth.CanAccessNotificationAsync(50, cust100Actor));
        Assert.False(await auth.CanAccessNotificationAsync(50, cust200Actor));

        // Feedback submission
        Assert.True(await auth.CanSubmitCustomerFeedbackAsync(100, cust100Actor));
        Assert.False(await auth.CanSubmitCustomerFeedbackAsync(100, cust200Actor));
    }

    // =========================================================================
    // SECTION 4: Application Services Integration Tests
    // =========================================================================

    [Fact]
    public async Task CustomerPortalApplicationService_RegisterAndFetchVehicles()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PortalAppDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Customers.Add(new Customer { Id = 100, FullName = "Nguyễn Văn Portal", Type = "INDIVIDUAL" });
        db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "TOYOTA", Name = "Toyota" });
        db.VehicleModels.AddRange(
            new VehicleModel { Id = 1, BrandId = 1, ModelName = "Camry" },
            new VehicleModel { Id = 2, BrandId = 1, ModelName = "Corolla" },
            new VehicleModel { Id = 3, BrandId = 1, ModelName = "Deleted", IsDeleted = true });
        await db.SaveChangesAsync();

        var auth = new CustomerPortalAuthorizationService(db);
        var appService = new CustomerPortalApplicationService(db, auth);
        var custActor = new UserActorContext(100, "Customer", "CUSTOMER", null, IsAdmin: false);

        // 1. Register vehicle
        var cmd = new CustomerPortalVehicleUpsertCommand(
            LicensePlate: "51K-99999",
            ModelId: 1,
            Brand: "Toyota",
            Model: "Camry",
            ManufactureYear: 2022,
            Color: "Trắng",
            Vin: "1HGCR2F83HA123456",
            CurrentOdo: 15000
        );

        var registered = await appService.RegisterVehicleAsync(100, cmd, custActor);
        Assert.True(registered.Id > 0);
        Assert.Equal("51K-99999", registered.LicensePlate);
        Assert.True(registered.IsCurrentOwner);
        var vehicleRow = (await db.Vehicles.FindAsync(registered.Id))!;
        var ownershipRow = await db.CustomerVehicles.SingleAsync(cv => cv.VehicleId == registered.Id);
        Assert.Null(vehicleRow.CreatedBy);
        Assert.Null(vehicleRow.UpdatedBy);
        Assert.Null(ownershipRow.CreatedBy);
        Assert.Null(ownershipRow.UpdatedBy);

        // 2. Fetch customer vehicles
        var vehicles = await appService.GetCustomerVehiclesAsync(100, custActor);
        Assert.Single(vehicles);
        Assert.Equal("51K-99999", vehicles[0].LicensePlate);
        Assert.Equal("Camry", vehicles[0].Model);

        // A deleted or missing model cannot be used to register or change a vehicle.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            appService.RegisterVehicleAsync(100, cmd with { ModelId = 3, LicensePlate = "51K-99998" }, custActor));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            appService.UpdateVehicleAsync(100, registered.Id, cmd with { ModelId = 999 }, custActor));
        var updated = await appService.UpdateVehicleAsync(100, registered.Id, cmd with { ModelId = 2 }, custActor);
        Assert.Equal("Corolla", updated!.Model);
        Assert.Equal(2, (await db.Vehicles.FindAsync(registered.Id))!.ModelId);
        Assert.Null(vehicleRow.UpdatedBy);

        // 3. Registering same plate under a different customer fails
        var foreignActor = new UserActorContext(200, "Customer", "CUSTOMER", null, IsAdmin: false);
        db.Customers.Add(new Customer { Id = 200, FullName = "Người Lạ", Type = "INDIVIDUAL" });
        (await db.CustomerVehicles.SingleAsync(cv => cv.VehicleId == registered.Id)).IsCurrentOwner = null;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            appService.RegisterVehicleAsync(200, cmd, foreignActor));
    }

    [Fact]
    public async Task CustomerPortalApplicationService_ServiceOrderLinkContext_ResolvesTimeline()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PortalContextDb_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Customers.Add(new Customer { Id = 100, FullName = "Chủ Đơn Hàng", Type = "INDIVIDUAL" });
        db.ServiceOrders.Add(new ServiceOrder
        {
            Id = 10,
            Code = "SO-2026-00010",
            CustomerId = 100,
            VehicleId = 1,
            Status = "IN_PROGRESS",
            ExecuteBranchId = 1
        });
        await db.SaveChangesAsync();

        var auth = new CustomerPortalAuthorizationService(db);
        var appService = new CustomerPortalApplicationService(db, auth);
        var custActor = new UserActorContext(100, "Customer", "CUSTOMER", null, IsAdmin: false);

        var context = await appService.GetServiceOrderLinkContextAsync(100, 10, custActor);
        Assert.NotNull(context);
        Assert.Equal("SO-2026-00010", context.OrderCode);
        Assert.Equal(CustomerPortalDomainRules.StageRepairing, context.StageCode);
        Assert.Equal("Đang sửa", context.StageLabel);
        Assert.Equal(6, context.Timeline.Count);
        Assert.Equal("current", context.Timeline.First(t => t.Code == CustomerPortalDomainRules.StageRepairing).State);
    }

    [Fact]
    public async Task NotificationApplicationService_LifecycleAndReminders()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"NotifAppDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Customers.Add(new Customer { Id = 100, FullName = "Khách Nhận Thông Báo", Type = "INDIVIDUAL" });
        db.Vehicles.Add(new Vehicle { Id = 1, LicensePlate = "51H-12345" });
        await db.SaveChangesAsync();

        var auth = new CustomerPortalAuthorizationService(db);
        var notifService = new NotificationApplicationService(db, auth);
        var staffActor = new UserActorContext(500, "Staff", "STAFF", BranchId: 1, IsAdmin: false);
        var custActor = new UserActorContext(100, "Customer", "CUSTOMER", null, IsAdmin: false);

        // 1. Create notification
        var createCmd = new NotificationCreateCommand(
            CustomerId: 100,
            Title: "Đơn sửa chữa hoàn tất",
            Content: "Xe của bạn đã sẵn sàng bàn giao.",
            LinkAction: "/my-garage-dashboard"
        );
        var notif = await notifService.CreateNotificationAsync(createCmd, staffActor);
        Assert.True(notif.Id > 0);
        Assert.False(notif.IsRead);

        // 2. Get customer notifications
        var page = await notifService.GetNotificationsAsync(100, new NotificationPageQuery(), custActor);
        Assert.Single(page.Items);
        Assert.Equal(1, page.UnreadCount);

        // 3. Mark read
        var readSuccess = await notifService.MarkNotificationReadAsync(100, notif.Id, custActor);
        Assert.True(readSuccess);

        // 4. Schedule and process maintenance reminder
        var schedCmd = new MaintenanceReminderScheduleCommand(
            CustomerId: 100,
            VehicleId: 1,
            ServiceOrderId: null,
            ReminderType: "Bảo dưỡng 10.000km",
            ScheduledAt: VietnamTime.Now.AddMinutes(-10), // due in the past
            ReminderMileage: 10000,
            EstimatedServices: "Thay dầu, kiểm tra phanh",
            Channel: "IN_APP"
        );
        var reminder = await notifService.ScheduleMaintenanceReminderAsync(schedCmd, staffActor);
        Assert.Equal(NotificationDomainRules.StatusScheduled, reminder.Status);

        var processed = await notifService.ProcessDueRemindersAsync(50);
        Assert.Equal(1, processed);

        var updatedReminder = await db.MaintenanceReminders.FindAsync(reminder.Id);
        Assert.Equal(NotificationDomainRules.StatusSent, updatedReminder!.Status);
        Assert.NotNull(updatedReminder.SentAt);
    }

    // =========================================================================
    // SECTION 5: Web API & Dual Routing Tests (TestWebApplicationFactory)
    // =========================================================================

    [Fact]
    public async Task Module10Controllers_EnforceDualRoutes_AcrossAllSevenControllers()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var client = factory.CreateClient();

        // Admin/Staff client token
        var staffToken = TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        // Reception chat requires a receptionist, even when an admin can use the other routes.
        using var receptionClient = factory.CreateClient();
        receptionClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "RECEPTIONIST", 1));

        // Customer client token
        using var custClient = factory.CreateClient();
        var custToken = TestWebApplicationFactory.CreateJwtToken(100, "Customer", "CUSTOMER", null);
        custClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", custToken);

        // 1. CustomerChatController dual routes (/api/chat vs /api/v1/chat)
        var chatRes1 = await custClient.GetAsync("/api/chat/my-conversations");
        var chatRes2 = await custClient.GetAsync("/api/v1/chat/my-conversations");
        Assert.Equal(chatRes1.StatusCode, chatRes2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, chatRes2.StatusCode);

        // 2. ReceptionChatController dual routes (/api/reception/chat vs /api/v1/reception/chat)
        var repRes1 = await receptionClient.GetAsync("/api/reception/chat/conversations");
        var repRes2 = await receptionClient.GetAsync("/api/v1/reception/chat/conversations");
        Assert.Equal(repRes1.StatusCode, repRes2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repRes2.StatusCode);

        // 3. ChatMessageController dual routes (/api/chat-messages vs /api/v1/chat-messages)
        var msgRes1 = await client.GetAsync("/api/chat-messages");
        var msgRes2 = await client.GetAsync("/api/v1/chat-messages");
        Assert.Equal(msgRes1.StatusCode, msgRes2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, msgRes2.StatusCode);

        // 4. ConversationController dual routes (/api/conversations vs /api/v1/conversations)
        var convRes1 = await client.GetAsync("/api/conversations");
        var convRes2 = await client.GetAsync("/api/v1/conversations");
        Assert.Equal(convRes1.StatusCode, convRes2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, convRes2.StatusCode);

        // 5. NotificationController dual routes (/api/notifications vs /api/v1/notifications)
        var notifRes1 = await client.GetAsync("/api/notifications");
        var notifRes2 = await client.GetAsync("/api/v1/notifications");
        Assert.Equal(notifRes1.StatusCode, notifRes2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, notifRes2.StatusCode);

        // 6. NotificationTemplateController dual routes (/api/notification-templates vs /api/v1/notification-templates)
        var tmplRes1 = await client.GetAsync("/api/notification-templates");
        var tmplRes2 = await client.GetAsync("/api/v1/notification-templates");
        Assert.Equal(tmplRes1.StatusCode, tmplRes2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, tmplRes2.StatusCode);

        // 7. MaintenanceRemindersController dual routes (/api/maintenance-reminders vs /api/v1/maintenance-reminders)
        var remRes1 = await client.PostAsync("/api/maintenance-reminders/process-due", null);
        var remRes2 = await client.PostAsync("/api/v1/maintenance-reminders/process-due", null);
        Assert.Equal(remRes1.StatusCode, remRes2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, remRes2.StatusCode);
    }

    [Fact]
    public async Task Module10Controllers_AnonymousCallers_RejectedWithUnauthorized()
    {
        using var factory = new TestWebApplicationFactory();
        using var anonClient = factory.CreateClient();

        var resChat = await anonClient.GetAsync("/api/v1/chat/my-conversations");
        Assert.Equal(HttpStatusCode.Unauthorized, resChat.StatusCode);

        var resRep = await anonClient.GetAsync("/api/v1/reception/chat/conversations");
        Assert.Equal(HttpStatusCode.Unauthorized, resRep.StatusCode);

        var resNotif = await anonClient.GetAsync("/api/v1/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, resNotif.StatusCode);

        var resRem = await anonClient.PostAsync("/api/v1/maintenance-reminders/process-due", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resRem.StatusCode);
    }
}
