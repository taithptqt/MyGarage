using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.Application.Ai;
using MyGarage.Application.Ai.DTOs;
using MyGarage.Application.Authorization;
using MyGarage.Common;
using MyGarage.Data;
using MyGarage.Domain.Rules;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class AiAndDispatchModuleTests
{
    // =========================================================================
    // SECTION 1: AiDomainRules Unit Tests
    // =========================================================================

    [Theory]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(365, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(366, false)]
    public void ForecastRules_ValidatesForecastDaysBounds(int days, bool expected)
    {
        Assert.Equal(expected, AiDomainRules.IsValidForecastDays(days));
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.75, true)]
    [InlineData(1.0, true)]
    [InlineData(-0.01, false)]
    [InlineData(1.01, false)]
    public void ForecastRules_ValidatesConfidenceScoreBounds(double score, bool expected)
    {
        Assert.Equal(expected, AiDomainRules.IsValidConfidenceScore((decimal)score));
        Assert.True(AiDomainRules.IsValidConfidenceScore((decimal?)null));
    }

    [Theory]
    [InlineData(10, 25, 5, 20)]
    [InlineData(50, 20, 5, 0)]
    [InlineData(-3, 15, 5, 20)]
    [InlineData(0, 0, 0, 0)]
    public void ForecastRules_CalculatesRecommendedOrderQuantity(
        int currentStock,
        int forecastDemand,
        int safetyStock,
        int expected)
    {
        var qty = AiDomainRules.CalculateRecommendedOrderQuantity(currentStock, forecastDemand, safetyStock);
        Assert.Equal(expected, qty);
    }

    [Fact]
    public void KnowledgeBaseRules_ValidatesTitleContentAndCategory()
    {
        var (ok1, err1) = AiDomainRules.ValidateKnowledgeEntry(
            "Quy trình bảo dưỡng phanh ABS",
            "Kiểm tra má phanh, đĩa phanh và dầu phanh định kỳ mỗi 10.000km.",
            "MAINTENANCE");
        Assert.True(ok1);
        Assert.Null(err1);

        var (okEmptyTitle, _) = AiDomainRules.ValidateKnowledgeEntry("", "Nội dung hợp lệ", "GENERAL");
        Assert.False(okEmptyTitle);

        var (okLongTitle, _) = AiDomainRules.ValidateKnowledgeEntry(new string('A', 251), "Nội dung hợp lệ", "GENERAL");
        Assert.False(okLongTitle);

        var (okEmptyContent, _) = AiDomainRules.ValidateKnowledgeEntry("Tiêu đề hợp lệ", "   ", "GENERAL");
        Assert.False(okEmptyContent);

        var (okLongContent, _) = AiDomainRules.ValidateKnowledgeEntry("Tiêu đề hợp lệ", new string('B', 20001), "GENERAL");
        Assert.False(okLongContent);

        var (okEmptyCategory, _) = AiDomainRules.ValidateKnowledgeEntry("Tiêu đề hợp lệ", "Nội dung hợp lệ", "");
        Assert.False(okEmptyCategory);
    }

    [Fact]
    public void ModelConfigRules_ValidatesProviderParametersAndMasksApiKey()
    {
        Assert.True(AiDomainRules.IsValidProvider("OPENAI"));
        Assert.True(AiDomainRules.IsValidProvider("gemini"));
        Assert.True(AiDomainRules.IsValidProvider("AZURE_OPENAI"));
        Assert.True(AiDomainRules.IsValidProvider("ANTHROPIC"));
        Assert.True(AiDomainRules.IsValidProvider("LOCAL"));
        Assert.False(AiDomainRules.IsValidProvider("UNKNOWN_PROVIDER"));

        var (validCfg, _) = AiDomainRules.ValidateModelConfig("OPENAI", "gpt-4o-mini", 0.7m, 4096);
        Assert.True(validCfg);

        var (badTemp, _) = AiDomainRules.ValidateModelConfig("OPENAI", "gpt-4o-mini", 2.5m, 4096);
        Assert.False(badTemp);

        var (badTokens, _) = AiDomainRules.ValidateModelConfig("OPENAI", "gpt-4o-mini", 0.7m, 200000);
        Assert.False(badTokens);

        var (badModel, _) = AiDomainRules.ValidateModelConfig("OPENAI", " ", 0.7m, 4096);
        Assert.False(badModel);

        Assert.Null(AiDomainRules.MaskApiKey(null));
        Assert.Null(AiDomainRules.MaskApiKey("   "));
        Assert.Equal("****", AiDomainRules.MaskApiKey("short7"));
        Assert.Equal("****7890", AiDomainRules.MaskApiKey("sk-proj-1234567890"));
    }

    [Fact]
    public void SmartDispatchRules_EnforcesStatusTransitions()
    {
        Assert.True(AiDomainRules.CanTransitionDispatchStatus("PENDING", "APPLIED"));
        Assert.True(AiDomainRules.CanTransitionDispatchStatus("PENDING", "REJECTED"));
        Assert.True(AiDomainRules.CanTransitionDispatchStatus("PENDING", "EXPIRED"));
        Assert.True(AiDomainRules.CanTransitionDispatchStatus(null, "APPLIED"));

        // Idempotent same-status transitions
        Assert.True(AiDomainRules.CanTransitionDispatchStatus("APPLIED", "APPLIED"));
        Assert.True(AiDomainRules.CanTransitionDispatchStatus("REJECTED", "REJECTED"));

        // Terminal state mutations forbidden
        Assert.False(AiDomainRules.CanTransitionDispatchStatus("APPLIED", "REJECTED"));
        Assert.False(AiDomainRules.CanTransitionDispatchStatus("REJECTED", "APPLIED"));
        Assert.False(AiDomainRules.CanTransitionDispatchStatus("EXPIRED", "APPLIED"));
        Assert.False(AiDomainRules.CanTransitionDispatchStatus("PENDING", "INVALID_STATE"));
    }

    [Fact]
    public void ChatbotRules_EnforcesSessionRoleAndImageUploadConstraints()
    {
        Assert.True(AiDomainRules.CanCustomerSendMessageToSession("ACTIVE", allowSendToLockedSession: false));
        Assert.False(AiDomainRules.CanCustomerSendMessageToSession("LOCKED", allowSendToLockedSession: false));
        Assert.True(AiDomainRules.CanCustomerSendMessageToSession("LOCKED", allowSendToLockedSession: true));
        Assert.False(AiDomainRules.CanCustomerSendMessageToSession("HANDED_OFF", allowSendToLockedSession: true));
        Assert.False(AiDomainRules.CanCustomerSendMessageToSession("CLOSED", allowSendToLockedSession: true));

        Assert.True(AiDomainRules.IsValidCustomerMessageRole("USER"));
        Assert.True(AiDomainRules.IsValidCustomerMessageRole("user"));
        Assert.False(AiDomainRules.IsValidCustomerMessageRole("ASSISTANT"));
        Assert.False(AiDomainRules.IsValidCustomerMessageRole("SYSTEM"));

        Assert.True(AiDomainRules.ValidateChatbotImageUpload("engine_leak.jpg", 512 * 1024).IsValid);
        Assert.True(AiDomainRules.ValidateChatbotImageUpload("dashboard.webp", 8 * 1024 * 1024).IsValid);
        Assert.False(AiDomainRules.ValidateChatbotImageUpload("script.exe", 1024).IsValid);
        Assert.False(AiDomainRules.ValidateChatbotImageUpload("empty.png", 0).IsValid);
        Assert.False(AiDomainRules.ValidateChatbotImageUpload("oversized.png", 8 * 1024 * 1024 + 1).IsValid);
    }

    // =========================================================================
    // SECTION 2: AiAuthorizationService Tests
    // =========================================================================

    [Fact]
    public async Task AiAuthorizationService_EnforcesRoleBranchAndSessionOwnership()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"AiAuthDb_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Customers.AddRange(
            new Customer { Id = 100, FullName = "Khách 100", Type = "INDIVIDUAL" },
            new Customer { Id = 200, FullName = "Khách 200", Type = "INDIVIDUAL" }
        );
        db.ChatbotSessions.Add(new ChatbotSession
        {
            Id = 50,
            CustomerId = 100,
            SessionToken = "tok-50",
            Status = "ACTIVE",
            CreatedAt = VietnamTime.Now,
            UpdatedAt = VietnamTime.Now,
            LastActivityAt = VietnamTime.Now
        });
        await db.SaveChangesAsync();

        var auth = new AiAuthorizationService(db);

        var adminActor = new UserActorContext(201, "Staff", "ADMIN", BranchId: 1, IsAdmin: true);
        var staffBranch1 = new UserActorContext(200, "Staff", "MANAGER", BranchId: 1, IsAdmin: false);
        var staffBranch2 = new UserActorContext(202, "Staff", "MANAGER", BranchId: 2, IsAdmin: false);
        var cust100Actor = new UserActorContext(100, "Customer", "CUSTOMER", BranchId: null, IsAdmin: false);
        var cust200Actor = new UserActorContext(200, "Customer", "CUSTOMER", BranchId: null, IsAdmin: false);

        // Model Configs & Knowledge Base: Admin only
        Assert.True(await auth.CanManageAiModelConfigsAsync(adminActor));
        Assert.False(await auth.CanManageAiModelConfigsAsync(staffBranch1));
        Assert.False(await auth.CanManageAiModelConfigsAsync(cust100Actor));

        Assert.True(await auth.CanManageAiKnowledgeBaseAsync(adminActor));
        Assert.False(await auth.CanManageAiKnowledgeBaseAsync(staffBranch1));
        Assert.False(await auth.CanManageAiKnowledgeBaseAsync(cust100Actor));

        // Forecasts & Smart Dispatching: Admin or matching branch Staff
        Assert.True(await auth.CanAccessAiForecastsAsync(1, adminActor));
        Assert.True(await auth.CanAccessAiForecastsAsync(2, adminActor));
        Assert.True(await auth.CanAccessAiForecastsAsync(1, staffBranch1));
        Assert.True(await auth.CanAccessAiForecastsAsync(null, staffBranch1));
        Assert.False(await auth.CanAccessAiForecastsAsync(1, staffBranch2));
        Assert.False(await auth.CanAccessAiForecastsAsync(1, cust100Actor));

        Assert.True(await auth.CanAccessSmartDispatchingAsync(1, adminActor));
        Assert.True(await auth.CanAccessSmartDispatchingAsync(1, staffBranch1));
        Assert.False(await auth.CanAccessSmartDispatchingAsync(1, staffBranch2));
        Assert.False(await auth.CanAccessSmartDispatchingAsync(1, cust100Actor));

        // Chatbot Session ownership
        Assert.True(await auth.CanAccessChatbotSessionAsync(100, 50, cust100Actor));
        Assert.False(await auth.CanAccessChatbotSessionAsync(200, 50, cust200Actor));
        Assert.False(await auth.CanAccessChatbotSessionAsync(100, 50, cust200Actor));
        Assert.True(await auth.CanAccessChatbotSessionAsync(100, 50, adminActor));
    }

    // =========================================================================
    // SECTION 3: AiApplicationService Integration Tests
    // =========================================================================

    [Fact]
    public async Task AiApplicationService_ManagesForecastsKnowledgeModelConfigsAndDispatch()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"AiAppDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Branches.Add(new Branch { Id = 1, Name = "Chi nhánh 1", Status = "ACTIVE" });
        db.Vehicles.Add(new Vehicle { Id = 10, LicensePlate = "51A-88888", IsDeleted = false });
        db.AiSmartDispatchings.Add(new AiSmartDispatching
        {
            Id = 5,
            BranchId = 1,
            Date = DateOnly.FromDateTime(VietnamTime.Now),
            Recommendation = "Ưu tiên Khoang 2 cho xe bảo dưỡng nhanh",
            EfficiencyScore = 0.92m,
            Status = "PENDING",
            CreatedAt = VietnamTime.Now
        });
        await db.SaveChangesAsync();

        var auth = new AiAuthorizationService(db);
        var service = new AiApplicationService(db, auth);

        var adminActor = new UserActorContext(201, "Staff", "ADMIN", BranchId: 1, IsAdmin: true);
        var staffBranch1 = new UserActorContext(200, "Staff", "MANAGER", BranchId: 1, IsAdmin: false);
        var staffBranch2 = new UserActorContext(202, "Staff", "MANAGER", BranchId: 2, IsAdmin: false);

        // 1. Forecast create & update
        var forecast = await service.CreateForecastAsync(
            new AiForecastUpsertCommand(
                VehicleId: 10,
                Type: "BRAKE_PAD_REPLACEMENT",
                PredictedDate: DateOnly.FromDateTime(VietnamTime.Now.AddDays(30)),
                ConfidenceScore: 0.88m,
                BranchId: 1),
            staffBranch1);

        Assert.True(forecast.Id > 0);
        Assert.Equal(0.88m, forecast.ConfidenceScore);

        var updatedForecast = await service.UpdateForecastAsync(
            forecast.Id,
            new AiForecastUpsertCommand(
                VehicleId: 10,
                Type: "BRAKE_PAD_REPLACEMENT",
                PredictedDate: DateOnly.FromDateTime(VietnamTime.Now.AddDays(45)),
                ConfidenceScore: 0.93m,
                BranchId: 1),
            staffBranch1);
        Assert.NotNull(updatedForecast);
        Assert.Equal(0.93m, updatedForecast!.ConfidenceScore);

        // 2. Knowledge Base create & update (Admin only)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateKnowledgeEntryAsync(
                new AiKnowledgeBaseUpsertCommand("Chủ đề", "Nội dung"),
                staffBranch1));

        var kb = await service.CreateKnowledgeEntryAsync(
            new AiKnowledgeBaseUpsertCommand(
                Topic: "Chính sách bảo hành ắc quy",
                Content: "Bảo hành 12 tháng hoặc 20.000km tùy điều kiện nào đến trước.",
                Category: "WARRANTY",
                SourceUrl: "https://mygarage.vn/kb/battery-warranty",
                IsDynamic: true),
            adminActor);
        Assert.True(kb.Id > 0);
        Assert.True(kb.IsDynamic);

        // 3. Model Config create & update (masks API key, never leaks raw key)
        var modelCfg = await service.CreateModelConfigAsync(
            new AiModelConfigUpsertCommand(
                Provider: "openai",
                ModelName: "gpt-4o-mini",
                ApiKey: "sk-secret-key-9876",
                MaxTokens: 4096,
                Temperature: 0.4m,
                IsActive: true),
            adminActor);

        Assert.Equal("OPENAI", modelCfg.Provider);
        Assert.True(modelCfg.HasApiKey);
        Assert.Equal("****9876", modelCfg.MaskedApiKey);

        // Updating without new ApiKey preserves existing key
        var updatedCfg = await service.UpdateModelConfigAsync(
            modelCfg.Id,
            new AiModelConfigUpsertCommand(
                Provider: "OPENAI",
                ModelName: "gpt-4o",
                ApiKey: null,
                MaxTokens: 8192,
                Temperature: 0.2m,
                IsActive: true),
            adminActor);
        Assert.NotNull(updatedCfg);
        Assert.Equal("gpt-4o", updatedCfg!.ModelName);
        Assert.True(updatedCfg.HasApiKey);
        Assert.Equal("****9876", updatedCfg.MaskedApiKey);

        // 4. Smart Dispatching confirm/reject workflow
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ApplyDispatchSuggestionAsync(5, staffBranch2));

        var applied = await service.ApplyDispatchSuggestionAsync(5, staffBranch1);
        Assert.NotNull(applied);
        Assert.Equal(AiDomainRules.DispatchStatusApplied, applied!.Status);

        // Cannot reject after already APPLIED
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejectDispatchSuggestionAsync(5, staffBranch1));
    }

    // =========================================================================
    // SECTION 4: Web API Dual Routing Tests (7 Controllers)
    // =========================================================================

    [Fact]
    public async Task Module11Controllers_EnforceDualRoutes_AcrossAllSevenControllers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            return Task.CompletedTask;
        });

        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiKnowledgePageReader>();
            services.AddScoped<IAiKnowledgePageReader, FakeKnowledgePageReader>();

            services.RemoveAll<IAiModelConfigPageReader>();
            services.AddScoped<IAiModelConfigPageReader, FakeModelConfigPageReader>();

            services.RemoveAll<IDispatchListReader>();
            services.AddScoped<IDispatchListReader, FakeDispatchListReader>();
        }));

        using var adminClient = app.CreateClient();
        var adminToken = TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var customerClient = app.CreateClient();
        var customerToken = TestWebApplicationFactory.CreateJwtToken(100, "Customer", "CUSTOMER", null);
        customerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        // 1. AiForecastController (/api/ai-forecasts vs /api/v1/ai-forecasts)
        var fcRes1 = await adminClient.GetAsync("/api/ai-forecasts");
        var fcRes2 = await adminClient.GetAsync("/api/v1/ai-forecasts");
        Assert.Equal(HttpStatusCode.OK, fcRes1.StatusCode);
        Assert.Equal(fcRes1.StatusCode, fcRes2.StatusCode);

        // 2. AiKnowledgeBaseController (/api/ai-knowledge-bases vs /api/v1/ai-knowledge-bases)
        var kbRes1 = await adminClient.GetAsync("/api/ai-knowledge-bases");
        var kbRes2 = await adminClient.GetAsync("/api/v1/ai-knowledge-bases");
        Assert.Equal(HttpStatusCode.OK, kbRes1.StatusCode);
        Assert.Equal(kbRes1.StatusCode, kbRes2.StatusCode);

        // 3. AiKnowledgePageController (/api/ai-knowledge-bases/page vs /api/v1/ai-knowledge-bases/page)
        var kbpRes1 = await adminClient.GetAsync("/api/ai-knowledge-bases/page");
        var kbpRes2 = await adminClient.GetAsync("/api/v1/ai-knowledge-bases/page");
        Assert.Equal(HttpStatusCode.OK, kbpRes1.StatusCode);
        Assert.Equal(kbpRes1.StatusCode, kbpRes2.StatusCode);

        // 4. AiModelConfigController (/api/ai-model-configs vs /api/v1/ai-model-configs)
        var mcRes1 = await adminClient.GetAsync("/api/ai-model-configs");
        var mcRes2 = await adminClient.GetAsync("/api/v1/ai-model-configs");
        Assert.Equal(HttpStatusCode.OK, mcRes1.StatusCode);
        Assert.Equal(mcRes1.StatusCode, mcRes2.StatusCode);

        // 5. AiModelConfigPageController (/api/ai-model-configs/page vs /api/v1/ai-model-configs/page)
        var mcpRes1 = await adminClient.GetAsync("/api/ai-model-configs/page");
        var mcpRes2 = await adminClient.GetAsync("/api/v1/ai-model-configs/page");
        Assert.Equal(HttpStatusCode.OK, mcpRes1.StatusCode);
        Assert.Equal(mcpRes1.StatusCode, mcpRes2.StatusCode);

        // 6. AiSmartDispatchingController (/api/ai-smart-dispatchings vs /api/v1/ai-smart-dispatchings)
        var sdRes1 = await adminClient.GetAsync("/api/ai-smart-dispatchings");
        var sdRes2 = await adminClient.GetAsync("/api/v1/ai-smart-dispatchings");
        Assert.Equal(HttpStatusCode.OK, sdRes1.StatusCode);
        Assert.Equal(sdRes1.StatusCode, sdRes2.StatusCode);

        // 7. ChatbotController (/api/chatbot/sessions vs /api/v1/chatbot/sessions)
        var cbRes1 = await customerClient.PostAsJsonAsync("/api/chatbot/sessions", new { });
        var cbRes2 = await customerClient.PostAsJsonAsync("/api/v1/chatbot/sessions", new { });
        Assert.Equal(HttpStatusCode.Created, cbRes1.StatusCode);
        Assert.Equal(cbRes1.StatusCode, cbRes2.StatusCode);
    }

    [Fact]
    public async Task Module11Controllers_AnonymousCallers_RejectedWithUnauthorized()
    {
        await using var factory = new TestWebApplicationFactory();
        using var anonClient = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/v1/ai-forecasts")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/v1/ai-knowledge-bases")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/v1/ai-knowledge-bases/page")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/v1/ai-model-configs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/v1/ai-model-configs/page")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/v1/ai-smart-dispatchings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.PostAsJsonAsync("/api/v1/chatbot/sessions", new { })).StatusCode);
    }

    private sealed class FakeKnowledgePageReader : IAiKnowledgePageReader
    {
        public Task<(IReadOnlyList<AiKnowledgeItem> Items, int Total)> ReadAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AiKnowledgeItem> items =
            [
                new AiKnowledgeItem(1, "Bảo dưỡng định kỳ", "Nội dung hướng dẫn", null, false, null)
            ];
            return Task.FromResult((items, 1));
        }
    }

    private sealed class FakeModelConfigPageReader : IAiModelConfigPageReader
    {
        public Task<(IReadOnlyList<AiModelConfigItem> Items, int Total)> ReadAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AiModelConfigItem> items =
            [
                new AiModelConfigItem(1, "OPENAI", true, "gpt-4o-mini", 4096, 0.5m, true)
            ];
            return Task.FromResult((items, 1));
        }
    }

    private sealed class FakeDispatchListReader : IDispatchListReader
    {
        public Task<(IReadOnlyList<DispatchRow> Items, int Total)> ReadAsync(
            int? page,
            int? pageSize,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<DispatchRow> items =
            [
                new DispatchRow(1, 1, new DateOnly(2026, 1, 1), "Gợi ý điều phối", 0.9m, "PENDING", null)
            ];
            return Task.FromResult((items, 1));
        }
    }
}
