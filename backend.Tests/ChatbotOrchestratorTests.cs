using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class ChatbotOrchestratorTests
{
    [Fact]
    public async Task OrchestrateAsync_AffirmingBookingOffer_StartsBookingAndCarriesSessionPrefill()
    {
        await using var ctx = CreateContext(nameof(OrchestrateAsync_AffirmingBookingOffer_StartsBookingAndCarriesSessionPrefill));
        ctx.Customers.Add(new Customer
        {
            Id = 101,
            FullName = "Customer One",
            Phone = "0900000000",
            Type = "CUSTOMER",
            IsActive = true,
            IsDeleted = false
        });
        await ctx.SaveChangesAsync();

        var chatbotService = new ChatbotService(
            ctx,
            new ChatbotRequestValidator(new ChatbotToolAllowlist()),
            new NoOpChatbotRateLimitHook(),
            NullLogger<ChatbotService>.Instance);

        var session = await chatbotService.CreateSessionAsync(
            101,
            501,
            new CreateChatbotSessionRequestDto());

        await chatbotService.SaveMessagesAsync(
            101,
            new SaveChatbotMessageRequestDto
            {
                SessionId = session.Id,
                Role = nameof(ChatbotMessageRole.USER),
                Content = "dat lich ngay mai luc 9h vi dieu hoa khong mat"
            });

        await chatbotService.SaveMessagesAsync(
            101,
            new SaveChatbotMessageRequestDto
            {
                SessionId = session.Id,
                Role = nameof(ChatbotMessageRole.ASSISTANT),
                Content = "De khac phuc tinh trang nay, ban co the tham khao dich vu **Noi soi & Tay o dan lanh dieu hoa** cua chung toi. Dich vu nay co gia 800.000 VND va mat khoang 75 phut de thuc hien.\n\nBan co muon dat lich cho dich vu nay khong?",
                SessionState = new ChatbotSessionStateUpdateRequestDto
                {
                    CurrentIntent = nameof(ChatbotIntentType.CONSULTATION),
                    IntentState = "{\"flow\":{\"intent\":\"CONSULTATION\",\"step\":\"confirm_follow_up\"}}"
                }
            });

        var bookingWorkflow = new RecordingBookingWorkflowService();
        var sut = new ChatbotOrchestrator(
            chatbotService,
            new IntentDetector(),
            new PassThroughGuardrailValidator(),
            bookingWorkflow,
            new NoOpChatbotHandoffService(),
            new NoOpChatbotRetrievalService(),
            new NoOpChatbotAnswerComposer(),
            new FixedPromptBuilder(),
            new NoOpFunctionCallingRouter(),
            new NoOpChatbotLlmPromptBuilder(),
            new NoOpChatbotLlmGateway(),
            new EmptyChatbotToolDefinitionRegistry(),
            NullLogger<ChatbotOrchestrator>.Instance);

        var reply = await sut.OrchestrateAsync(
            101,
            new OrchestrateChatbotRequestDto
            {
                SessionId = session.Id,
                UserMessage = "ok"
            });

        Assert.Equal(nameof(ChatbotIntentType.BOOKING), reply.Intent);
        Assert.Equal("CONSULTATION_BOOKING_HANDOFF", reply.Strategy);
        Assert.NotNull(reply.BookingWorkflow);
        Assert.Equal(1, bookingWorkflow.CallCount);
        Assert.Equal("ok", bookingWorkflow.LastUserMessage);

        using var doc = JsonDocument.Parse(bookingWorkflow.LastIntentStateJson!);
        var flow = doc.RootElement.GetProperty("flow");
        Assert.Equal(nameof(ChatbotIntentType.BOOKING), flow.GetProperty("intent").GetString());
        Assert.Equal("gather_booking_info", flow.GetProperty("step").GetString());

        var booking = doc.RootElement.GetProperty("booking");
        Assert.Equal(0, booking.GetProperty("Step").GetInt32());
        Assert.True(booking.GetProperty("AutoConfirmRequested").GetBoolean());

        var prefill = booking.GetProperty("PrefillInput").GetString();
        Assert.NotNull(prefill);
        Assert.Contains("Noi soi & Tay o dan lanh dieu hoa", prefill!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ngay mai", prefill, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("9h", prefill, StringComparison.OrdinalIgnoreCase);

        var updatedSession = await chatbotService.GetSessionByIdAsync(101, session.Id);
        Assert.NotNull(updatedSession);
        Assert.Equal(nameof(ChatbotIntentType.BOOKING), updatedSession!.CurrentIntent);
    }

    [Fact]
    public async Task OrchestrateAsync_AffirmingMaintenanceCheckOffer_WithMultipleVehicles_ListsVehiclesFromDatabase()
    {
        await using var ctx = CreateContext(nameof(OrchestrateAsync_AffirmingMaintenanceCheckOffer_WithMultipleVehicles_ListsVehiclesFromDatabase));
        SeedCustomerWithMultipleVehicles(ctx);
        await ctx.SaveChangesAsync();

        var chatbotService = new ChatbotService(
            ctx,
            new ChatbotRequestValidator(new ChatbotToolAllowlist()),
            new NoOpChatbotRateLimitHook(),
            NullLogger<ChatbotService>.Instance);

        var session = await chatbotService.CreateSessionAsync(
            101,
            501,
            new CreateChatbotSessionRequestDto());

        await chatbotService.SaveMessagesAsync(
            101,
            new SaveChatbotMessageRequestDto
            {
                SessionId = session.Id,
                Role = nameof(ChatbotMessageRole.USER),
                Content = "dieu hoa yeu"
            });

        await chatbotService.SaveMessagesAsync(
            101,
            new SaveChatbotMessageRequestDto
            {
                SessionId = session.Id,
                Role = nameof(ChatbotMessageRole.ASSISTANT),
                Content = "Dieu hoa yeu co the do nhieu nguyen nhan khac nhau. MyGarage co the ho tro ban kiem tra va sua chua van de nay. Ban co muon dat lich hen kiem tra dieu hoa cho xe cua minh khong?",
                SessionState = new ChatbotSessionStateUpdateRequestDto
                {
                    CurrentIntent = nameof(ChatbotIntentType.CONSULTATION),
                    IntentState = "{\"flow\":{\"intent\":\"CONSULTATION\",\"step\":\"confirm_follow_up\"}}"
                }
            });

        var llmGateway = new CountingChatbotLlmGateway();
        var bookingWorkflow = new ChatbotBookingWorkflowService(
            ctx,
            new FakeAppointmentService(),
            new FakeVoucherResolver(),
            new SystemClock(),
            NullLogger<ChatbotBookingWorkflowService>.Instance);
        var sut = new ChatbotOrchestrator(
            chatbotService,
            new IntentDetector(),
            new PassThroughGuardrailValidator(),
            bookingWorkflow,
            new NoOpChatbotHandoffService(),
            new NoOpChatbotRetrievalService(),
            new NoOpChatbotAnswerComposer(),
            new FixedPromptBuilder(),
            new NoOpFunctionCallingRouter(),
            new NoOpChatbotLlmPromptBuilder(),
            llmGateway,
            new EmptyChatbotToolDefinitionRegistry(),
            NullLogger<ChatbotOrchestrator>.Instance);

        var reply = await sut.OrchestrateAsync(
            101,
            new OrchestrateChatbotRequestDto
            {
                SessionId = session.Id,
                UserMessage = "ok"
            });

        Assert.Equal(nameof(ChatbotIntentType.BOOKING), reply.Intent);
        Assert.Equal("CONSULTATION_BOOKING_HANDOFF", reply.Strategy);
        Assert.NotNull(reply.BookingWorkflow);
        Assert.Equal("IDENTIFY_OR_CREATE_VEHICLE", reply.BookingWorkflow!.Step);
        Assert.True(reply.BookingWorkflow.RequiresUserInput);
        Assert.True(reply.BookingWorkflow.VehicleOptions.Count >= 2);
        Assert.Contains("51A12345", reply.AssistantReply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("30G99999", reply.AssistantReply, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, llmGateway.CallCount);
    }

    private static GarageContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new GarageContext(options, new HttpContextAccessor());
    }

    private static void SeedCustomerWithMultipleVehicles(GarageContext ctx)
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
        ctx.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "FORD", Name = "Ford", IsActive = true, IsDeleted = false });
        ctx.VehicleBrands.Add(new VehicleBrand { Id = 2, Code = "HONDA", Name = "Honda", IsActive = true, IsDeleted = false });
        ctx.VehicleModels.Add(new VehicleModel { Id = 901, BrandId = 1, ModelName = "Ranger", IsDeleted = false });
        ctx.VehicleModels.Add(new VehicleModel { Id = 902, BrandId = 2, ModelName = "City", IsDeleted = false });
        ctx.Vehicles.Add(new Vehicle { Id = 1001, LicensePlate = "51A12345", ModelId = 901, IsDeleted = false });
        ctx.Vehicles.Add(new Vehicle { Id = 1002, LicensePlate = "30G99999", ModelId = 902, IsDeleted = false });
        ctx.CustomerVehicles.Add(new CustomerVehicle { Id = 2001, CustomerId = 101, VehicleId = 1001, IsCurrentOwner = true, IsDeleted = false });
        ctx.CustomerVehicles.Add(new CustomerVehicle { Id = 2002, CustomerId = 101, VehicleId = 1002, IsCurrentOwner = true, IsDeleted = false });
        ctx.Branches.Add(new Branch { Id = 501, Name = "Central", Status = "ACTIVE", IsDeleted = false, Hotline = "1900" });
    }

    private sealed class RecordingBookingWorkflowService : IChatbotBookingWorkflowService
    {
        public int CallCount { get; private set; }

        public string? LastIntentStateJson { get; private set; }

        public string? LastUserMessage { get; private set; }

        public Task<ChatbotBookingWorkflowResponseDto> RunAsync(
            long customerId,
            long sessionId,
            long? sessionBranchId,
            string intentStateJson,
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastIntentStateJson = intentStateJson;
            LastUserMessage = userMessage;

            return Task.FromResult(new ChatbotBookingWorkflowResponseDto
            {
                Step = "IDENTIFY_OR_CREATE_VEHICLE",
                RequiresUserInput = true,
                UpdatedIntentState = intentStateJson
            });
        }
    }

    private sealed class PassThroughGuardrailValidator : IGuardrailValidator
    {
        public GuardrailValidationResult Validate(string input) => new()
        {
            IsAllowed = true,
            IsRejected = false,
            ShouldHandoff = false,
            SanitizedInput = input
        };
    }

    private sealed class NoOpChatbotRetrievalService : IChatbotRetrievalService
    {
        public Task<ChatbotRetrievalResult> RetrieveAsync(long customerId, long? branchId, string question, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatbotRetrievalResult());
    }

    private sealed class NoOpChatbotAnswerComposer : IChatbotAnswerComposer
    {
        public Task<string> ComposeAsync(ChatbotRetrievalResult retrieval, long? branchId, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }

    private sealed class FixedPromptBuilder : IPromptBuilder
    {
        public PromptBuildResult Build(string userInput, IntentDetectionResult intent, string intentStateJson, GuardrailValidationResult guardrail)
            => new()
            {
                SystemPrompt = "system",
                UserPrompt = userInput
            };
    }

    private sealed class NoOpFunctionCallingRouter : IFunctionCallingRouter
    {
        public Task<FunctionRoutingResult> RouteAsync(long customerId, long? branchId, string intent, string intentStateJson, PromptBuildResult prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(new FunctionRoutingResult());
    }

    private sealed class NoOpChatbotLlmPromptBuilder : IChatbotLlmPromptBuilder
    {
        public IReadOnlyList<ChatbotLlmMessage> BuildMessages(string systemPrompt, string userPrompt, IReadOnlyList<ChatbotMessageResponseDto> history, IReadOnlyList<ChatbotToolExecutionRecord>? toolResults = null)
            => [];
    }

    private sealed class NoOpChatbotLlmGateway : IChatbotLlmGateway
    {
        public Task<ChatbotLlmGatewayResponse> GenerateAsync(ChatbotLlmGatewayRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatbotLlmGatewayResponse
            {
                Succeeded = false,
                FailureCode = "NOT_USED"
            });
    }

    private sealed class CountingChatbotLlmGateway : IChatbotLlmGateway
    {
        public int CallCount { get; private set; }

        public Task<ChatbotLlmGatewayResponse> GenerateAsync(ChatbotLlmGatewayRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ChatbotLlmGatewayResponse
            {
                Succeeded = false,
                FailureCode = "NOT_USED"
            });
        }
    }

    private sealed class EmptyChatbotToolDefinitionRegistry : IChatbotToolDefinitionRegistry
    {
        public IReadOnlyList<ChatbotToolDefinition> GetToolDefinitions() => [];

        public Task<ChatbotToolExecutionResult> ExecuteAsync(ChatbotToolExecutionRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class NoOpChatbotHandoffService : IChatbotHandoffService
    {
        public Task<ChatbotHandoffResult> InitiateAsync(long customerId, long? preferredBranchId, string userMessage, string intent, string intentStateJson, decimal confidence, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class NoOpChatbotRateLimitHook : IChatbotRateLimitHook
    {
        public Task EnsureCanCreateSessionAsync(long customerId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task EnsureCanSendMessageAsync(long customerId, long sessionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeVoucherResolver : IChatbotVoucherResolverService
    {
        public Task<ChatbotVoucherResolveResult> ResolveBestVoucherAsync(ChatbotVoucherResolveRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatbotVoucherResolveResult
            {
                IsApplied = false,
                ReasonCode = "NO_VALID_VOUCHER",
                Message = "No voucher in test setup."
            });
    }

    private sealed class FakeAppointmentService : IAppointmentService
    {
        public Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(long branchId, DateOnly date, string? requestedServiceIds = null, string? bookingFlow = null, int vehicleCount = 1, bool isParallel = true)
            => Task.FromResult<IEnumerable<AvailableSlotResponseDto>>([]);

        public Task<Appointment> BookAsync(long customerId, BookAppointmentRequestDto dto) => throw new NotImplementedException();
        public Task<BookingGroupResponseDto> BookBatchForCustomerAsync(long actorUserId, long targetCustomerId, BookAppointmentBatchRequestDto dto) => throw new NotImplementedException();
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
}
