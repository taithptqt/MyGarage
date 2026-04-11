using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Data;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class ChatbotP0SafetyTests
{
    [Fact]
    public void CustomerSessionJson_DoesNotExposeInternalChatbotStateOrToolPayloads()
    {
        var dto = new ChatbotSessionResponseDto
        {
            Id = 5001,
            SessionToken = "cbs_safe",
            CustomerId = 101,
            Status = "ACTIVE",
            CurrentIntent = "BOOKING",
            IntentState = "{\"booking\":{\"vehicleId\":1001}}",
            HandoffConversationId = 7001,
            HandoffSummary = "{\"intentState\":{\"secret\":true}}",
            Messages =
            [
                new ChatbotMessageResponseDto
                {
                    Id = 9001,
                    SessionId = 5001,
                    Role = "ASSISTANT",
                    Content = "Hello",
                    ToolName = "get_customer_vouchers",
                    ToolArgs = "{\"customerId\":101}",
                    ToolResult = "{\"internal\":\"payload\"}",
                    SentimentScore = 0.1m,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };

        var json = JsonSerializer.Serialize(dto);

        Assert.DoesNotContain("CustomerId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IntentState", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HandoffSummary", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HandoffConversationId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ToolArgs", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ToolResult", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SessionId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CustomerOrchestrateJson_DoesNotExposeInternalStateOrToolPayloads()
    {
        var dto = new OrchestratedChatbotReplyDto
        {
            SessionId = 5001,
            SessionToken = "cbs_safe",
            Intent = "BOOKING",
            Confidence = 0.98m,
            Strategy = "WORKFLOW",
            AssistantReply = "Booking summary...",
            UpdatedIntentState = "{\"booking\":{\"customerId\":101,\"tool\":\"internal\"}}",
            BookingWorkflow = new ChatbotBookingWorkflowResponseDto
            {
                Step = "FINALIZE_BOOKING",
                Prompt = "Booking summary...",
                SlotHoldToken = "hold_secret",
                UpdatedIntentState = "{\"booking\":{\"slotHoldToken\":\"internal\"}}"
            },
            FunctionCalls =
            [
                new OrchestratedFunctionCallDto
                {
                    ToolName = "get_customer_vouchers",
                    ToolArgs = "{\"customerId\":101,\"voucherId\":42}"
                }
            ]
        };

        var json = JsonSerializer.Serialize(dto);

        Assert.DoesNotContain("UpdatedIntentState", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FunctionCalls", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("get_customer_vouchers", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ToolArgs", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customerId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("slotHoldToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hold_secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSessionAsync_IgnoresClientSuppliedInternalState()
    {
        await using var ctx = CreateContext(nameof(CreateSessionAsync_IgnoresClientSuppliedInternalState));
        ctx.Customers.Add(new Customer
        {
            Id = 101,
            FullName = "Safe Customer",
            Type = "INDIVIDUAL",
            IsActive = true,
            IsDeleted = false
        });
        await ctx.SaveChangesAsync();

        var sut = new ChatbotService(
            ctx,
            new ChatbotRequestValidator(new ChatbotToolAllowlist()),
            new NoOpChatbotRateLimitHook(),
            NullLogger<ChatbotService>.Instance);

        var result = await sut.CreateSessionAsync(
            101,
            null,
            new MyGarage.DTOs.Requests.Chatbot.CreateChatbotSessionRequestDto
            {
                CurrentIntent = "BOOKING",
                IntentState = "{\"booking\":{\"step\":\"FinalizeBooking\",\"autoConfirmRequested\":true}}"
            });

        var persisted = await ctx.ChatbotSessions.AsNoTracking().SingleAsync(x => x.Id == result.Id);
        Assert.Null(result.CurrentIntent);
        Assert.Null(result.IntentState);
        Assert.Null(persisted.CurrentIntent);
        Assert.Null(persisted.IntentState);
    }

    [Theory]
    [InlineData("PROVIDER_API_KEY_MISSING")]
    [InlineData("MODEL_CONFIG_NOT_FOUND")]
    [InlineData("PROVIDER_ENDPOINT_NOT_CONFIGURED")]
    public void LlmGatewayFailureReply_DoesNotExposeInternalConfigurationDetails(string failureCode)
    {
        var method = typeof(ChatbotOrchestrator).GetMethod(
            "BuildLlmGatewayFailureReply",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var response = new ChatbotLlmGatewayResponse
        {
            Succeeded = false,
            FailureCode = failureCode,
            FailureMessage = "internal config failure"
        };

        var reply = Assert.IsType<string>(method!.Invoke(null, [response]));

        Assert.DoesNotContain("appsettings", reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AI_ModelConfigs", reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API key", reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AI", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Vui", reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolAllowlist_DoesNotExposeWriteToolsToLlm()
    {
        var allowlist = new ChatbotToolAllowlist();

        Assert.True(allowlist.IsAllowedForLlm("get_customer_vouchers"));
        Assert.True(allowlist.IsAllowedForLlm("get_available_slots"));
        Assert.False(allowlist.IsAllowedForLlm("update_booking_state"));
        Assert.False(allowlist.IsAllowedForLlm("confirm_appointment_booking"));
        Assert.False(allowlist.IsAllowedForLlm("create_service_order_draft"));
        Assert.False(allowlist.IsAllowedForLlm("create_retail_order_draft"));
    }

    [Fact]
    public async Task ServiceOrderDraft_RequiresServices()
    {
        await using var ctx = CreateContext(nameof(ServiceOrderDraft_RequiresServices));
        SeedCustomerVehicleAndBranches(ctx, includeExecuteBranch: true);
        var sut = CreateRegistry(ctx);

        var result = await sut.ExecuteAsync(new ChatbotToolExecutionRequest
        {
            CustomerId = 101,
            SessionId = 5001,
            BranchId = 501,
            ToolCall = new ChatbotLlmToolCall
            {
                ToolName = "create_service_order_draft",
                ToolArgs = """
                           {
                             "vehicleId": 1001,
                             "originBranchId": 501,
                             "executeBranchId": 502,
                             "customerNote": "Engine vibration"
                           }
                           """
            }
        });

        Assert.True(result.Succeeded);
        Assert.Contains("\"canProceed\":false", result.ToolResult);
        Assert.Contains("serviceIds", result.ToolResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ServiceOrderDraft_RequiresBothBranchesToExistAndBeActive()
    {
        await using var ctx = CreateContext(nameof(ServiceOrderDraft_RequiresBothBranchesToExistAndBeActive));
        SeedCustomerVehicleAndBranches(ctx, includeExecuteBranch: false);
        SeedService(ctx);
        var sut = CreateRegistry(ctx);

        var result = await sut.ExecuteAsync(new ChatbotToolExecutionRequest
        {
            CustomerId = 101,
            SessionId = 5001,
            BranchId = 501,
            ToolCall = new ChatbotLlmToolCall
            {
                ToolName = "create_service_order_draft",
                ToolArgs = """
                           {
                             "vehicleId": 1001,
                             "originBranchId": 501,
                             "executeBranchId": 502,
                             "serviceIds": [11],
                             "customerNote": "Brake check"
                           }
                           """
            }
        });

        Assert.True(result.Succeeded);
        Assert.Contains("\"canProceed\":false", result.ToolResult);
        Assert.Contains("BRANCH_NOT_FOUND_OR_INACTIVE", result.ToolResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetailOrderDraft_RequiresActiveBranch()
    {
        await using var ctx = CreateContext(nameof(RetailOrderDraft_RequiresActiveBranch));
        ctx.Branches.Add(new Branch { Id = 501, Name = "Closed Branch", Status = "CLOSED", IsDeleted = false });
        ctx.Products.Add(new Product
        {
            Id = 701,
            Sku = "OIL-701",
            Name = "Engine Oil",
            Unit = "BOTTLE",
            RetailPrice = 100000m,
            IsActive = true,
            IsDeleted = false
        });
        ctx.Warehouses.Add(new Warehouse
        {
            Id = 801,
            BranchId = 501,
            Code = "WH-501",
            Name = "Closed Warehouse",
            Type = "RETAIL",
            IsActive = true,
            IsDeleted = false
        });
        ctx.Inventories.Add(new Inventory
        {
            Id = 901,
            WarehouseId = 801,
            ProductId = 701,
            Quantity = 10m,
            ReservedQuantity = 0m
        });
        ctx.SaveChanges();

        var sut = CreateRegistry(ctx);

        var result = await sut.ExecuteAsync(new ChatbotToolExecutionRequest
        {
            CustomerId = 101,
            SessionId = 5001,
            BranchId = 501,
            ToolCall = new ChatbotLlmToolCall
            {
                ToolName = "create_retail_order_draft",
                ToolArgs = """
                           {
                             "branchId": 501,
                             "items": [{ "productId": 701, "quantity": 1 }]
                           }
                           """
            }
        });

        Assert.True(result.Succeeded);
        Assert.Contains("\"canProceed\":false", result.ToolResult);
        Assert.Contains("BRANCH_NOT_FOUND_OR_INACTIVE", result.ToolResult, StringComparison.OrdinalIgnoreCase);
    }

    private static ChatbotToolDefinitionRegistry CreateRegistry(GarageContext ctx)
        => new(
            new ChatbotToolAllowlist(),
            null!,
            null!,
            null!,
            null!,
            null!,
            ctx,
            NullLogger<ChatbotToolDefinitionRegistry>.Instance);

    private static GarageContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new GarageContext(options, new HttpContextAccessor());
    }

    private static void SeedCustomerVehicleAndBranches(GarageContext ctx, bool includeExecuteBranch)
    {
        ctx.Customers.Add(new Customer
        {
            Id = 101,
            FullName = "Customer One",
            Phone = "0900000000",
            Type = "CUSTOMER",
            IsActive = true,
            IsDeleted = false
        });
        ctx.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "BRANDA", Name = "BrandA", IsActive = true, IsDeleted = false });
        ctx.VehicleModels.Add(new VehicleModel { Id = 901, BrandId = 1, ModelName = "ModelX", IsDeleted = false });
        ctx.Vehicles.Add(new Vehicle { Id = 1001, LicensePlate = "51A12345", ModelId = 901, IsDeleted = false });
        ctx.CustomerVehicles.Add(new CustomerVehicle { Id = 2001, CustomerId = 101, VehicleId = 1001, IsCurrentOwner = true, IsDeleted = false });
        ctx.Branches.Add(new Branch { Id = 501, Name = "Origin", Status = "ACTIVE", IsDeleted = false });
        if (includeExecuteBranch)
        {
            ctx.Branches.Add(new Branch { Id = 502, Name = "Execute", Status = "ACTIVE", IsDeleted = false });
        }
        ctx.SaveChanges();
    }

    private static void SeedService(GarageContext ctx)
    {
        ctx.ServiceCategories.Add(new ServiceCategory { Id = 1, Code = "MAINT", Name = "Maintenance", IsActive = true, IsDeleted = false });
        ctx.StandardServices.Add(new StandardService { Id = 11, Code = "SVC11", Name = "Brake check", CategoryId = 1, BasePrice = 100000, StandardMinutes = 45, IsActive = true, IsDeleted = false });
        ctx.SaveChanges();
    }

    private sealed class NoOpChatbotRateLimitHook : IChatbotRateLimitHook
    {
        public Task EnsureCanCreateSessionAsync(long customerId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task EnsureCanSendMessageAsync(long customerId, long sessionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

