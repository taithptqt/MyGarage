using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Hubs;
using MyGarage.Interfaces;
using MyGarage.Repositories;
using MyGarage.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using PayOS;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Options;

var builder = WebApplication.CreateBuilder(args);

static string ResolvePayOsCredential(
    IConfiguration config,
    IHostEnvironment env,
    string configurationKey,
    string environmentVariableName,
    string developmentPlaceholder)
{
    var value = config[configurationKey] ?? Environment.GetEnvironmentVariable(environmentVariableName);
    if (!string.IsNullOrWhiteSpace(value))
    {
        return value.Trim();
    }

    if (env.IsDevelopment())
    {
        return developmentPlaceholder;
    }

    throw new InvalidOperationException(
        $"PayOS: missing {configurationKey} (or env {environmentVariableName}). " +
        "Set values in appsettings, user secrets, or environment variables.");
}

const string FrontendCorsPolicy = "FrontendCorsPolicy";
const string AdminStaffOnlyPolicy = "AdminStaffOnly";

if (builder.Environment.IsDevelopment())
{
    // Keep local development stable on the requested ports.
    builder.WebHost.UseUrls("https://localhost:7009", "http://localhost:5062");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton<ISlotHoldService, SlotHoldService>();
builder.Services.AddDbContext<GarageContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GarageDbConnection")));
builder.Services.AddScoped<IGarageContext>(provider => provider.GetRequiredService<GarageContext>());


builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.SetIsOriginAllowed(origin =>
                origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("https://localhost:", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(origin, "https://accounts.google.com", StringComparison.OrdinalIgnoreCase))
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped(typeof(IGenericService<>), typeof(GenericService<>));
builder.Services.AddScoped<IAccountingCrudService, AccountingCrudService>();
builder.Services.AddScoped<IPayrollBatchCalculationService, PayrollBatchCalculationService>();
builder.Services.AddSingleton<IOrderStore, OrderService>();
builder.Services.AddSingleton<IOrderTransactionStore, OrderTransactionService>();
builder.Services.AddSingleton<IOrderInvoiceStore, OrderInvoiceService>();
builder.Services.AddSingleton<ITransferStore, TransferService>();

builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IVehicleHistoryService, VehicleHistoryService>();

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddScoped<IServiceOrderRepository, ServiceOrderRepository>();
builder.Services.AddScoped<IServiceOrderCustomerLifecycleNotifier, ServiceOrderCustomerLifecycleNotifier>();
builder.Services.AddScoped<IMaintenanceReminderService, MaintenanceReminderService>();
builder.Services.AddScoped<IServiceOrderService, ServiceOrderService>();
builder.Services.AddScoped<ICustomerPortalService, CustomerPortalService>();
builder.Services.AddScoped<IQuotationWorkflowService, QuotationWorkflowService>();

builder.Services.AddScoped<IServiceOrderDetailRepository, ServiceOrderDetailRepository>();
builder.Services.AddScoped<IServiceOrderDetailService, ServiceOrderDetailService>();

builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IAppointmentStatusPolicy, AppointmentStatusPolicy>();
builder.Services.AddScoped<IAppointmentDetailAssembler, AppointmentDetailAssembler>();
builder.Services.AddScoped<IGroupSessionStatusPolicy, GroupSessionStatusPolicy>();
builder.Services.AddScoped<IGroupSessionService, GroupSessionService>();
builder.Services.AddHostedService<AppointmentDispatchBackgroundService>();
builder.Services.AddHostedService<AppointmentReminderBackgroundService>();
builder.Services.AddHostedService<AppointmentInsuranceUploadCleanupBackgroundService>();
builder.Services.AddHostedService<ServiceOrderInsuranceUploadCleanupBackgroundService>();

builder.Services.AddSingleton<IValidateOptions<CustomerAutomationJobsOptions>, CustomerAutomationJobsOptionsValidator>();
builder.Services.AddOptions<CustomerAutomationJobsOptions>()
    .Bind(builder.Configuration.GetSection(CustomerAutomationJobsOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<LlmGatewayOptions>, LlmGatewayOptionsValidator>();
builder.Services.AddOptions<LlmGatewayOptions>()
    .Bind(builder.Configuration.GetSection(LlmGatewayOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddScoped<ICustomerAutomationJobProcessor, CustomerAutomationJobProcessor>();
builder.Services.AddHostedService<CustomerAutomationJobsBackgroundService>();

builder.Services.AddScoped<IStaffRepository, StaffRepository>();
builder.Services.AddScoped<IStaffService, StaffService>();

builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<ITeamService, TeamService>();

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();

builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

builder.Services.AddScoped<IServiceCategoryRepository, ServiceCategoryRepository>();
builder.Services.AddScoped<IServiceCategoryService, ServiceCategoryService>();

builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IInventoryAdminService, InventoryAdminService>();

builder.Services.AddScoped<IStaffScheduleRepository, StaffScheduleRepository>();
builder.Services.AddScoped<IStaffScheduleService, StaffScheduleService>();

builder.Services.AddScoped<IAiForecastRepository, AiForecastRepository>();
builder.Services.AddScoped<IAiForecastService, AiForecastService>();

builder.Services.AddScoped<IAiKnowledgeBaseRepository, AiKnowledgeBaseRepository>();
builder.Services.AddScoped<IAiKnowledgeBaseService, AiKnowledgeBaseService>();

builder.Services.AddScoped<IAiModelConfigRepository, AiModelConfigRepository>();
builder.Services.AddScoped<IAiModelConfigService, AiModelConfigService>();

builder.Services.AddScoped<IAiSmartDispatchingRepository, AiSmartDispatchingRepository>();
builder.Services.AddScoped<IAiSmartDispatchingService, AiSmartDispatchingService>();

builder.Services.AddScoped<IApprovalRequestRepository, ApprovalRequestRepository>();
builder.Services.AddScoped<IApprovalRequestService, ApprovalRequestService>();

builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

builder.Services.AddScoped<IBayRepository, BayRepository>();
builder.Services.AddScoped<IBayService, BayService>();

builder.Services.AddScoped<IBranchRepository, BranchRepository>();
builder.Services.AddScoped<IBranchService, BranchService>();

builder.Services.AddScoped<IBranchExceptionRepository, BranchExceptionRepository>();
builder.Services.AddScoped<IBranchExceptionService, BranchExceptionService>();

builder.Services.AddScoped<IBranchOperatingHourRepository, BranchOperatingHourRepository>();
builder.Services.AddScoped<IBranchOperatingHourService, BranchOperatingHourService>();

builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
builder.Services.AddScoped<IChatMessageService, ChatMessageService>();

builder.Services.AddScoped<ICommissionRuleRepository, CommissionRuleRepository>();
builder.Services.AddScoped<ICommissionRuleService, CommissionRuleService>();

builder.Services.AddScoped<ICommissionTransactionRepository, CommissionTransactionRepository>();
builder.Services.AddScoped<ICommissionTransactionService, CommissionTransactionService>();

builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IChatV2Service, ChatV2Service>();
builder.Services.AddScoped<IChatbotToolAllowlist, ChatbotToolAllowlist>();
builder.Services.AddScoped<IChatbotRequestValidator, ChatbotRequestValidator>();
builder.Services.AddSingleton<IChatbotRateLimitHook, InMemoryChatbotRateLimitHook>();
builder.Services.AddScoped<IChatbotPricingInventoryGuard, ChatbotPricingInventoryGuard>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IChatbotImageUploadService, ChatbotImageUploadService>();
builder.Services.AddScoped<IIntentDetector, IntentDetector>();
builder.Services.AddScoped<IGuardrailValidator, GuardrailValidator>();
builder.Services.AddScoped<IVisionAnalyzer, HeuristicVisionAnalyzer>();
builder.Services.AddScoped<IFileSecurityScanner, NoOpFileSecurityScanner>();
builder.Services.AddScoped<IChatbotBookingWorkflowService, ChatbotBookingWorkflowService>();
builder.Services.AddScoped<IChatbotHandoffService, ChatbotHandoffService>();
builder.Services.AddScoped<IChatbotVoucherResolverService, ChatbotVoucherResolverService>();
builder.Services.AddScoped<IChatbotLoyaltyStatusLookupService, ChatbotLoyaltyStatusLookupService>();
builder.Services.AddScoped<IChatbotVoucherStatusLookupService, ChatbotVoucherStatusLookupService>();
builder.Services.AddScoped<IChatbotRetrievalService, ChatbotRetrievalService>();
builder.Services.AddScoped<IChatbotAnswerComposer, ChatbotAnswerComposer>();
builder.Services.AddScoped<IPromptBuilder, PromptBuilder>();
builder.Services.AddSingleton<IChatbotLlmPromptBuilder, ChatbotLlmPromptBuilder>();
builder.Services.AddSingleton<IChatbotLlmResponseNormalizer, ChatbotLlmResponseNormalizer>();
builder.Services.AddScoped<IChatbotToolDefinitionRegistry, ChatbotToolDefinitionRegistry>();
builder.Services.AddScoped<IChatbotLlmGateway, ChatbotLlmGateway>();
builder.Services.AddSingleton<IChatbotLlmProviderClient, OpenAiCompatibleChatbotLlmProviderClient>();
builder.Services.AddScoped<IFunctionCallingRouter, SecureFunctionCallingRouter>();
builder.Services.AddScoped<IChatbotOrchestrator, ChatbotOrchestrator>();

builder.Services.AddScoped<ICustomerFeedbackRepository, CustomerFeedbackRepository>();
builder.Services.AddScoped<ICustomerFeedbackService, CustomerFeedbackService>();

builder.Services.AddScoped<ICustomerTierRepository, CustomerTierRepository>();
builder.Services.AddScoped<ICustomerTierService, CustomerTierService>();

builder.Services.AddScoped<ICustomerVehicleRepository, CustomerVehicleRepository>();
builder.Services.AddScoped<ICustomerVehicleService, CustomerVehicleService>();

builder.Services.AddScoped<ICustomerVoucherRepository, CustomerVoucherRepository>();
builder.Services.AddScoped<ICustomerVoucherService, CustomerVoucherService>();

builder.Services.AddScoped<IDeliveryRepository, DeliveryRepository>();
builder.Services.AddScoped<IDeliveryService, DeliveryService>();

builder.Services.AddScoped<IDiagnosisRecommendationRepository, DiagnosisRecommendationRepository>();
builder.Services.AddScoped<IDiagnosisRecommendationService, DiagnosisRecommendationService>();

builder.Services.AddScoped<IDiagnosisRepository, DiagnosisRepository>();
builder.Services.AddScoped<IDiagnosisService, DiagnosisService>();

builder.Services.AddScoped<IExpenseLogRepository, ExpenseLogRepository>();
builder.Services.AddScoped<IExpenseLogService, ExpenseLogService>();

builder.Services.AddScoped<IGatePassRepository, GatePassRepository>();
builder.Services.AddScoped<IGatePassService, GatePassService>();

builder.Services.AddScoped<IInsuranceCompanyRepository, InsuranceCompanyRepository>();
builder.Services.AddScoped<IInsuranceCompanyService, InsuranceCompanyService>();

builder.Services.AddScoped<IInventoryAuditRepository, InventoryAuditRepository>();
builder.Services.AddScoped<IInventoryAuditService, InventoryAuditService>();

builder.Services.AddScoped<IInventoryAuditDetailRepository, InventoryAuditDetailRepository>();
builder.Services.AddScoped<IInventoryAuditDetailService, InventoryAuditDetailService>();

builder.Services.AddScoped<IInvoiceDetailRepository, InvoiceDetailRepository>();
builder.Services.AddScoped<IInvoiceDetailService, InvoiceDetailService>();

builder.Services.AddScoped<IMaterialRequestRepository, MaterialRequestRepository>();
builder.Services.AddScoped<IMaterialRequestService, MaterialRequestService>();

builder.Services.AddScoped<IMaterialRequestDetailRepository, MaterialRequestDetailRepository>();
builder.Services.AddScoped<IMaterialRequestDetailService, MaterialRequestDetailService>();

builder.Services.AddScoped<IMaterialReturnRepository, MaterialReturnRepository>();
builder.Services.AddScoped<IMaterialReturnService, MaterialReturnService>();

builder.Services.AddScoped<IModelSpecificRuleRepository, ModelSpecificRuleRepository>();
builder.Services.AddScoped<IModelSpecificRuleService, ModelSpecificRuleService>();

builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
builder.Services.AddScoped<INotificationTemplateService, NotificationTemplateService>();

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

builder.Services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
builder.Services.AddScoped<IPaymentMethodService, PaymentMethodService>();

builder.Services.AddScoped<IProductImageRepository, ProductImageRepository>();
builder.Services.AddScoped<IProductImageService, ProductImageService>();
builder.Services.AddScoped<ICloudStorageService, AzureBlobStorageService>();

builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();

builder.Services.AddScoped<IPurchaseOrderItemRepository, PurchaseOrderItemRepository>();
builder.Services.AddScoped<IPurchaseOrderItemService, PurchaseOrderItemService>();

builder.Services.AddScoped<IQcInspectionRepository, QcInspectionRepository>();
builder.Services.AddScoped<IQcInspectionService, QcInspectionService>();

builder.Services.AddScoped<IRetailOrderRepository, RetailOrderRepository>();
builder.Services.AddScoped<IRetailOrderService, RetailOrderService>();

builder.Services.AddScoped<IRetailOrderItemRepository, RetailOrderItemRepository>();
builder.Services.AddScoped<IRetailOrderItemService, RetailOrderItemService>();

builder.Services.AddScoped<IRetailReturnRepository, RetailReturnRepository>();
builder.Services.AddScoped<IRetailReturnService, RetailReturnService>();

builder.Services.AddScoped<IRetailReturnItemRepository, RetailReturnItemRepository>();
builder.Services.AddScoped<IRetailReturnItemService, RetailReturnItemService>();

builder.Services.AddScoped<IRevenueSnapshotRepository, RevenueSnapshotRepository>();
builder.Services.AddScoped<IRevenueSnapshotService, RevenueSnapshotService>();

builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRoleService, RoleService>();

builder.Services.AddScoped<IServiceOrderDiscountRepository, ServiceOrderDiscountRepository>();
builder.Services.AddScoped<IServiceOrderDiscountService, ServiceOrderDiscountService>();

builder.Services.AddScoped<IServiceOrderSnapshotRepository, ServiceOrderSnapshotRepository>();
builder.Services.AddScoped<IServiceOrderSnapshotService, ServiceOrderSnapshotService>();

builder.Services.AddScoped<IServiceRefundRepository, ServiceRefundRepository>();
builder.Services.AddScoped<IServiceRefundService, ServiceRefundService>();

builder.Services.AddScoped<IServiceStaffAssignmentRepository, ServiceStaffAssignmentRepository>();
builder.Services.AddScoped<IServiceStaffAssignmentService, ServiceStaffAssignmentService>();

builder.Services.AddScoped<IShiftRepository, ShiftRepository>();
builder.Services.AddScoped<IShiftService, ShiftService>();

builder.Services.AddScoped<IShiftClosingRepository, ShiftClosingRepository>();
builder.Services.AddScoped<IShiftClosingService, ShiftClosingService>();

builder.Services.AddScoped<IStaffTierRepository, StaffTierRepository>();
builder.Services.AddScoped<IStaffTierService, StaffTierService>();
builder.Services.AddScoped<ISalaryCoefficientRepository, SalaryCoefficientRepository>();
builder.Services.AddScoped<ISalaryCoefficientService, SalaryCoefficientService>();

builder.Services.AddScoped<IStandardServiceRepository, StandardServiceRepository>();
builder.Services.AddScoped<IStandardServiceService, StandardServiceService>();

builder.Services.AddScoped<IPackageRepository, PackageRepository>();
builder.Services.AddScoped<IPackageService, PackageService>();

builder.Services.AddScoped<IStockTransactionRepository, StockTransactionRepository>();
builder.Services.AddScoped<IStockTransactionService, StockTransactionService>();

builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ISupplierService, SupplierService>();

builder.Services.AddScoped<IVehicleBrandRepository, VehicleBrandRepository>();
builder.Services.AddScoped<IVehicleBrandService, VehicleBrandService>();

builder.Services.AddScoped<IVehicleConditionLogRepository, VehicleConditionLogRepository>();
builder.Services.AddScoped<IVehicleConditionLogService, VehicleConditionLogService>();

builder.Services.AddScoped<IVehicleInsuranceRepository, VehicleInsuranceRepository>();
builder.Services.AddScoped<IVehicleInsuranceService, VehicleInsuranceService>();

builder.Services.AddScoped<IVehicleModelRepository, VehicleModelRepository>();
builder.Services.AddScoped<IVehicleModelService, VehicleModelService>();

builder.Services.AddScoped<IVoucherRepository, VoucherRepository>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddScoped<IVoucherCampaignRepository, VoucherCampaignRepository>();
builder.Services.AddScoped<IVoucherCampaignService, VoucherCampaignService>();
builder.Services.AddScoped<IVoucherApprovalRequestRepository, VoucherApprovalRequestRepository>();
builder.Services.AddScoped<IVoucherApprovalRequestService, VoucherApprovalRequestService>();
builder.Services.AddScoped<IVoucherRedemptionRepository, VoucherRedemptionRepository>();
builder.Services.AddScoped<IVoucherRedemptionService, VoucherRedemptionService>();

builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();

builder.Services.AddScoped<IWorkSlotRepository, WorkSlotRepository>();
builder.Services.AddScoped<IWorkSlotService, WorkSlotService>();

// Authentication & JWT Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthCredentialRecoveryValidator, AuthCredentialRecoveryValidator>();
builder.Services.AddScoped<IAuthSecurityStampValidator, AuthSecurityStampValidator>();
builder.Services.AddSingleton<IAuthRateLimitHook, InMemoryAuthRateLimitHook>();
builder.Services.AddScoped<IPaymentConfirmationService, PaymentConfirmationService>();
builder.Services.AddHttpClient();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForMyGarageBackendThatIsLongEnoughToWork";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "MyGarageServer";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "MyGarageClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrWhiteSpace(accessToken) &&
                path.StartsWithSegments("/hubs/chat"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            if (context.Principal == null)
            {
                context.Fail("Invalid session.");
                return;
            }

            using var scope = context.HttpContext.RequestServices.CreateScope();
            var validator = scope.ServiceProvider.GetRequiredService<IAuthSecurityStampValidator>();
            var isValid = await validator.ValidateAsync(context.Principal, context.HttpContext.RequestAborted);
            if (!isValid)
            {
                context.Fail("Session is no longer valid.");
            }
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminStaffOnlyPolicy, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var roleCode = context.User.FindFirst("role_code")?.Value;
            var roleName = context.User.FindFirst("role_name")?.Value;
            return string.Equals(roleCode, "ADMIN", StringComparison.OrdinalIgnoreCase)
                   || (!string.IsNullOrWhiteSpace(roleName) &&
                       roleName.Contains("admin", StringComparison.OrdinalIgnoreCase));
        });
    });
});

builder.Services.AddKeyedSingleton("OrderClient", (sp, key) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var env = sp.GetRequiredService<IHostEnvironment>();
    return new PayOSClient(new PayOSOptions
    {
        ClientId = ResolvePayOsCredential(config, env, "PayOS:ClientId", "PAYOS_CLIENT_ID", "dev-local-order-client-id"),
        ApiKey = ResolvePayOsCredential(config, env, "PayOS:ApiKey", "PAYOS_API_KEY", "dev-local-order-api-key"),
        ChecksumKey = ResolvePayOsCredential(config, env, "PayOS:ChecksumKey", "PAYOS_CHECKSUM_KEY", "dev-local-order-checksum-key"),
        LogLevel = LogLevel.Debug,
    });
});

builder.Services.AddKeyedSingleton("TransferClient", (sp, key) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var env = sp.GetRequiredService<IHostEnvironment>();
    return new PayOSClient(new PayOSOptions
    {
        ClientId = ResolvePayOsCredential(config, env, "PayOS:PayoutClientId", "PAYOS_PAYOUT_CLIENT_ID", "dev-local-payout-client-id"),
        ApiKey = ResolvePayOsCredential(config, env, "PayOS:PayoutApiKey", "PAYOS_PAYOUT_API_KEY", "dev-local-payout-api-key"),
        ChecksumKey = ResolvePayOsCredential(config, env, "PayOS:PayoutChecksumKey", "PAYOS_PAYOUT_CHECKSUM_KEY", "dev-local-payout-checksum-key"),
        LogLevel = LogLevel.Debug,
    });
});

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSignalR();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();
var frontendDistPath = ResolveFrontendDistPath(app.Environment.ContentRootPath);
var hasFrontendBuild = Directory.Exists(frontendDistPath);
PhysicalFileProvider? frontendFileProvider = hasFrontendBuild ? new PhysicalFileProvider(frontendDistPath) : null;

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (ShouldUseHttpsRedirection(builder.Configuration, app.Environment))
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles(); // Serve files from wwwroot (uploads, etc.)

if (hasFrontendBuild && frontendFileProvider is not null)
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = frontendFileProvider
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = frontendFileProvider
    });
}

app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

// Liveness endpoint — used by Docker healthcheck and load balancers.
// Returns 200 immediately; no auth, no DB round-trip.
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }))
   .AllowAnonymous()
   .ExcludeFromDescription(); // hide from Swagger

if (hasFrontendBuild)
{
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/swagger"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await context.Response.SendFileAsync(Path.Combine(frontendDistPath, "index.html"));
    });
}

app.Run();

static string ResolveFrontendDistPath(string contentRootPath)
{
    var rootsToCheck = new[]
    {
        contentRootPath,
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory
    };

    foreach (var root in rootsToCheck)
    {
        var current = new DirectoryInfo(root);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "frontend", "dist");
            var indexFile = Path.Combine(candidate, "index.html");
            if (File.Exists(indexFile))
            {
                return candidate;
            }

            current = current.Parent;
        }
    }

    return Path.Combine(contentRootPath, "frontend", "dist");
}

static bool ShouldUseHttpsRedirection(IConfiguration configuration, IHostEnvironment environment)
{
    if (environment.IsDevelopment())
    {
        return true;
    }

    var urls = configuration["ASPNETCORE_URLS"];
    if (!string.IsNullOrWhiteSpace(urls) &&
        urls.Contains("https://", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    var httpsPorts = configuration["ASPNETCORE_HTTPS_PORTS"];
    if (!string.IsNullOrWhiteSpace(httpsPorts))
    {
        return true;
    }

    var httpsPort = configuration["HTTPS_PORT"] ?? configuration["ASPNETCORE_HTTPS_PORT"];
    if (!string.IsNullOrWhiteSpace(httpsPort))
    {
        return true;
    }

    var kestrelHttpsUrl = configuration["Kestrel:Endpoints:Https:Url"];
    if (!string.IsNullOrWhiteSpace(kestrelHttpsUrl))
    {
        return true;
    }

    return false;
}

public partial class Program;
