using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Hubs;
using MyGarage.Interfaces;
using MyGarage.Repositories;
using MyGarage.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using PayOS;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Options;
using MyGarage.Middleware;
using MyGarage.OData;
using MyGarage.Security;
using Microsoft.AspNetCore.HttpOverrides;
using MyGarage.Application.Authorization;
using MyGarage.Application.Pricing;
using MyGarage.Application.Inventory;
using MyGarage.Application.Bays;
using MyGarage.Application.Purchasing;
using MyGarage.Application.Retail;
using MyGarage.Application.Vouchers;
using MyGarage.Application.Payments;
using MyGarage.Application.Invoices;
using MyGarage.Application.Accounting;
using MyGarage.Application.Customers;
using MyGarage.Application.Vehicles;
using MyGarage.Application.Catalog;
using MyGarage.Application.Payroll;
using MyGarage.Application.CustomerPortal;
using MyGarage.Application.Notifications;
using MyGarage.Application.Ai;
using MyGarage.Application.GatePasses;
using MyGarage.Extensions;

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
builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddDbContext<GarageContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("GarageDbConnection"),
        sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("MyGarage.Infrastructure");
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(15),
                errorNumbersToAdd: null);
        }));
builder.Services.AddScoped<IGarageContext>(provider => provider.GetRequiredService<GarageContext>());
builder.Services.AddDbContext<MyGarage.Infrastructure.Payroll.SalaryProfileReadDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GarageDbConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(15), null)));
builder.Services.AddScoped<MyGarage.Application.Payroll.ISalaryProfileListReader, MyGarage.Infrastructure.Payroll.SalaryProfileListReader>();
builder.Services.AddScoped<MyGarage.Application.Payroll.SalaryProfileListQuery>();
builder.Services.AddDbContext<MyGarage.Infrastructure.Ai.ForecastReadDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GarageDbConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(15), null)));
builder.Services.AddScoped<MyGarage.Application.Forecasts.IForecastListReader, MyGarage.Infrastructure.Ai.ForecastListReader>();
builder.Services.AddScoped<MyGarage.Application.Forecasts.ForecastListQuery>();
builder.Services.AddDbContext<MyGarage.Infrastructure.Ai.DispatchReadDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GarageDbConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(15), null)));
builder.Services.AddScoped<MyGarage.Application.Ai.IDispatchListReader, MyGarage.Infrastructure.Ai.DispatchListReader>();
builder.Services.AddScoped<MyGarage.Application.Ai.DispatchListQuery>();
builder.Services.AddDbContext<MyGarage.Infrastructure.Ai.DispatchBayReadDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GarageDbConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(15), null)));
builder.Services.AddScoped<MyGarage.Application.Ai.IDispatchBayCandidates, MyGarage.Infrastructure.Ai.DispatchBayCandidates>();
builder.Services.AddDbContext<MyGarage.Infrastructure.Ai.AiKnowledgeReadDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GarageDbConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(15), null)));
builder.Services.AddScoped<MyGarage.Application.Ai.IAiKnowledgePageReader, MyGarage.Infrastructure.Ai.AiKnowledgePageReader>();
builder.Services.AddScoped<MyGarage.Application.Ai.AiKnowledgePageQuery>();
builder.Services.AddScoped<MyGarage.Application.Ai.IAiKnowledgeCatalogReader, MyGarage.Infrastructure.Ai.AiKnowledgeCatalogReader>();
builder.Services.AddScoped<MyGarage.Application.Ai.AiKnowledgeCatalogQuery>();
builder.Services.AddDbContext<MyGarage.Infrastructure.GatePasses.GatePassReadDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GarageDbConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(15), null)));
builder.Services.AddScoped<MyGarage.Application.GatePasses.IGatePassListReader, MyGarage.Infrastructure.GatePasses.GatePassListReader>();
builder.Services.AddScoped<MyGarage.Application.GatePasses.GatePassListQuery>();
builder.Services.AddDbContext<MyGarage.Infrastructure.Audit.AuditReadDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GarageDbConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(15), null)));
builder.Services.AddScoped<MyGarage.Application.Audit.IAdminAuditReader, MyGarage.Infrastructure.Audit.AdminAuditReader>();
builder.Services.AddScoped<MyGarage.Application.Audit.AdminAuditQuery>();
builder.Services.AddDbContext<MyGarage.Infrastructure.Approvals.ApprovalReadDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GarageDbConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(15), null)));
builder.Services.AddScoped<MyGarage.Application.Approvals.IApprovalListReader, MyGarage.Infrastructure.Approvals.ApprovalListReader>();
builder.Services.AddScoped<MyGarage.Application.Approvals.ApprovalListQuery>();
builder.Services.AddDbContext<MyGarage.Infrastructure.Ai.AiModelConfigReadDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GarageDbConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(15), null)));
builder.Services.AddScoped<MyGarage.Application.Ai.IAiModelConfigPageReader, MyGarage.Infrastructure.Ai.AiModelConfigPageReader>();
builder.Services.AddScoped<MyGarage.Application.Ai.AiModelConfigPageQuery>();
builder.Services.AddDbContext<MyGarage.Infrastructure.Payments.PaymentReadDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GarageDbConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(15), null)));

var allowedCorsOrigins = ResolveAllowedCorsOrigins(builder.Configuration, builder.Environment);
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.SetIsOriginAllowed(origin =>
                IsAllowedCorsOrigin(origin, allowedCorsOrigins, builder.Environment))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("X-Total-Count", "X-Page", "X-Page-Size", "X-Total-Pages",
                  "X-Salary-Pending-Count", "X-Salary-Approved-Count");
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (!System.Net.IPAddress.TryParse(proxy, out var address))
            throw new InvalidOperationException($"Invalid trusted proxy address: {proxy}");
        options.KnownProxies.Add(address);
    }
});

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped(typeof(IGenericService<>), typeof(GenericService<>));
builder.Services.AddScoped<IAccountingCrudService, AccountingCrudService>();
builder.Services.AddScoped<IGrnSnapshotService, GrnSnapshotService>();
builder.Services.AddScoped<IPayrollBatchCalculationService, PayrollBatchCalculationService>();
builder.Services.AddSingleton<IOrderStore, OrderService>();
builder.Services.AddSingleton<IOrderTransactionStore, OrderTransactionService>();
builder.Services.AddSingleton<IOrderInvoiceStore, OrderInvoiceService>();

builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IVehicleHistoryService, VehicleHistoryService>();

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

builder.Services.AddAutoMapper(config =>
{
    MappingDepthLimit.Apply(config);
    var licenseKey = builder.Configuration["AutoMapper:LicenseKey"] ?? builder.Configuration["AUTOMAPPER_LICENSE_KEY"];
    if (!string.IsNullOrWhiteSpace(licenseKey)) config.LicenseKey = licenseKey;
}, AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddScoped<IServiceOrderRepository, ServiceOrderRepository>();
builder.Services.AddScoped<IServiceOrderCustomerLifecycleNotifier, ServiceOrderCustomerLifecycleNotifier>();
builder.Services.AddScoped<IMaintenanceReminderService, MaintenanceReminderService>();
builder.Services.AddScoped<IServiceOrderService, ServiceOrderService>();
builder.Services.AddScoped<IServiceOrderAuthorizationService, ServiceOrderAuthorizationService>();
builder.Services.AddScoped<ITechnicianProductivityService, TechnicianProductivityService>();
builder.Services.AddScoped<ICustomerPortalService, CustomerPortalService>();
builder.Services.AddScoped<IQuotationPricingCalculationService, QuotationPricingCalculationService>();
builder.Services.AddScoped<IQuotationAuthorizationService, QuotationAuthorizationService>();
builder.Services.AddScoped<IQuotationWorkflowService, QuotationWorkflowService>();

builder.Services.AddScoped<IServiceOrderDetailRepository, ServiceOrderDetailRepository>();
builder.Services.AddScoped<IServiceOrderDetailService, ServiceOrderDetailService>();

builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<MyGarage.Application.Invoices.IInvoiceViewDetailReader, InvoiceViewDetailReader>();
builder.Services.AddScoped<MyGarage.Application.Invoices.InvoiceViewDetailQuery>();
builder.Services.AddScoped<MyGarage.Application.Catalog.IServiceCategoryPageReader, ServiceCategoryPageReader>();
builder.Services.AddScoped<MyGarage.Application.Catalog.ServiceCategoryPageQuery>();
builder.Services.AddScoped<MyGarage.Application.Catalog.IStandardServicePageReader, StandardServicePageReader>();
builder.Services.AddScoped<MyGarage.Application.Catalog.StandardServicePageQuery>();

builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IAppointmentAuthorizationService, AppointmentAuthorizationService>();
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
builder.Services.AddScoped<ITimesheetService, TimesheetService>();

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
builder.Services.AddScoped<IBayAuthorizationService, BayAuthorizationService>();
builder.Services.AddScoped<IBayApplicationService, BayApplicationService>();
builder.Services.AddScoped<IInventoryAuthorizationService, InventoryAuthorizationService>();
builder.Services.AddScoped<IInventoryApplicationService, InventoryApplicationService>();
builder.Services.AddScoped<IPurchaseOrderAuthorizationService, PurchaseOrderAuthorizationService>();
builder.Services.AddScoped<IPurchaseOrderApplicationService, PurchaseOrderApplicationService>();
builder.Services.AddScoped<IGoodsReceiptApplicationService, GoodsReceiptApplicationService>();
builder.Services.AddScoped<IRetailOrderAuthorizationService, RetailOrderAuthorizationService>();
builder.Services.AddScoped<IRetailOrderApplicationService, RetailOrderApplicationService>();
builder.Services.AddScoped<IVoucherAuthorizationService, VoucherAuthorizationService>();
builder.Services.AddScoped<IVoucherApplicationService, VoucherApplicationService>();
builder.Services.AddScoped<IPaymentAuthorizationService, PaymentAuthorizationService>();
builder.Services.AddScoped<IPaymentApplicationService, PaymentApplicationService>();
builder.Services.AddScoped<IInvoiceAuthorizationService, InvoiceAuthorizationService>();
builder.Services.AddScoped<IInvoiceApplicationService, InvoiceApplicationService>();
builder.Services.AddScoped<IAccountingAuthorizationService, AccountingAuthorizationService>();
builder.Services.AddScoped<IAccountingApplicationService, AccountingApplicationService>();
builder.Services.AddScoped<ICustomerAuthorizationService, CustomerAuthorizationService>();
builder.Services.AddScoped<ICustomerApplicationService, CustomerApplicationService>();
builder.Services.AddScoped<IVehicleAuthorizationService, VehicleAuthorizationService>();
builder.Services.AddScoped<IVehicleApplicationService, VehicleApplicationService>();
builder.Services.AddScoped<ICatalogAuthorizationService, CatalogAuthorizationService>();
builder.Services.AddScoped<ICatalogApplicationService, CatalogApplicationService>();
builder.Services.AddScoped<IPayrollAuthorizationService, PayrollAuthorizationService>();
builder.Services.AddScoped<IPayrollApplicationService, PayrollApplicationService>();
builder.Services.AddScoped<ICustomerPortalAuthorizationService, CustomerPortalAuthorizationService>();
builder.Services.AddScoped<ICustomerPortalApplicationService, CustomerPortalApplicationService>();
builder.Services.AddScoped<INotificationApplicationService, NotificationApplicationService>();
builder.Services.AddScoped<IAiAuthorizationService, AiAuthorizationService>();
builder.Services.AddScoped<IAiApplicationService, AiApplicationService>();
builder.Services.AddScoped<ISharedOperationsAuthorizationService, SharedOperationsAuthorizationService>();
builder.Services.AddScoped<IGatePassAndApprovalApplicationService, GatePassAndApprovalApplicationService>();

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
builder.Services.AddScoped<IFileSecurityScanner, MagicBytesFileSecurityScanner>();
builder.Services.AddSingleton<IPiiAnonymizerService, PiiAnonymizerService>();
builder.Services.AddScoped<IODataQueryService, ODataQueryService>();
builder.Services.AddScoped<MyGarage.Application.Payments.IInvoiceQueryReader, InvoiceQueryReader>();
builder.Services.AddScoped<MyGarage.Application.Payments.InvoiceQuery>();
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
builder.Services.AddScoped<MyGarage.Application.Payments.IInvoiceTaxSummaryReader, InvoiceTaxSummaryReader>();
builder.Services.AddScoped<MyGarage.Application.Payments.InvoiceTaxSummaryQuery>();
builder.Services.AddScoped<MyGarage.Application.Payments.IInvoiceListPageReader, InvoiceListPageReader>();
builder.Services.AddScoped<MyGarage.Application.Payments.InvoiceListPageQuery>();
builder.Services.AddScoped<MyGarage.Application.Payments.ICustomerPaymentsReader, MyGarage.Infrastructure.Payments.CustomerPaymentsReader>();
builder.Services.AddScoped<MyGarage.Application.Payments.CustomerPaymentsQuery>();

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
builder.Services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();
builder.Services.AddSingleton<IAuthRateLimitHook, InMemoryAuthRateLimitHook>();
builder.Services.AddSingleton<IWriteRequestSpamGuard, InMemoryWriteRequestSpamGuard>();
builder.Services.AddScoped<IPaymentConfirmationService, PaymentConfirmationService>();
builder.Services.AddHttpClient();

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must be configured and at least 32 UTF-8 bytes long for HS256.");
}

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
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(context.Token) &&
                context.Request.Cookies.TryGetValue(AuthCookie.Name, out var cookieToken) &&
                !string.IsNullOrWhiteSpace(cookieToken))
            {
                context.Token = cookieToken;
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
            var blacklist = scope.ServiceProvider.GetRequiredService<ITokenBlacklistService>();

            var rawToken = context.SecurityToken switch
            {
                JwtSecurityToken jwt => jwt.RawData,
                Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jsonJwt => jsonJwt.EncodedToken,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(rawToken) &&
                context.Request.Cookies.TryGetValue(AuthCookie.Name, out var cookieToken))
            {
                rawToken = cookieToken;
            }

            if (await blacklist.IsTokenRevokedAsync(rawToken))
            {
                context.Fail("Session has been revoked.");
                return;
            }

            var role = context.Principal.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var userIdRaw = context.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var authTicksRaw = context.Principal.FindFirst("auth_time_ticks")?.Value;
            DateTime? issuedAtUtc = long.TryParse(authTicksRaw, out var ticks)
                ? new DateTime(ticks, DateTimeKind.Utc)
                : context.SecurityToken?.ValidFrom;
            if (long.TryParse(userIdRaw, out var userId) &&
                blacklist.IsUserRevoked(role, userId, issuedAtUtc))
            {
                context.Fail("Session has been invalidated.");
                return;
            }

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
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

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
            return string.Equals(roleCode, "ADMIN", StringComparison.OrdinalIgnoreCase);
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

// Add services to the container.

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string ResolvePartitionKey(HttpContext httpContext, string prefix)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var user = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anon";
        var actorType = httpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? "anonymous";
        return $"{prefix}:{actorType}:{user}:{ip}";
    }

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ResolvePartitionKey(httpContext, "auth"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("otp", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ResolvePartitionKey(httpContext, "otp"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 6,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("odata", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ResolvePartitionKey(httpContext, "odata"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    })
    .AddOData(options =>
        options.Select()
            .Filter()
            .OrderBy()
            .Count()
            .SetMaxTop(100)
            .AddRouteComponents("odata", MyGarageEdmModel.Create()));
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
app.UseExceptionHandler();
app.UseForwardedHeaders();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
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
app.UseMiddleware<CookieOriginValidationMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<WriteRequestSpamGuardMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

// Liveness & readiness endpoints — used by Docker healthcheck and load balancers.
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }))
   .AllowAnonymous()
   .ExcludeFromDescription(); // hide from Swagger

app.MapGet("/api/health/live", () => Results.Ok(new { status = "live", utc = DateTime.UtcNow }))
   .AllowAnonymous()
   .ExcludeFromDescription();

app.MapGet("/api/health/ready", async (GarageContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new { status = "ready", database = "connected", utc = DateTime.UtcNow })
        : Results.Json(new { status = "unready", database = "disconnected", utc = DateTime.UtcNow },
            statusCode: StatusCodes.Status503ServiceUnavailable);
})
   .AllowAnonymous()
   .ExcludeFromDescription();

if (hasFrontendBuild)
{
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Path.StartsWithSegments("/odata") ||
            context.Request.Path.StartsWithSegments("/swagger"))
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

static HashSet<string> ResolveAllowedCorsOrigins(IConfiguration configuration, IHostEnvironment environment)
{
    var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    var sectionOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    if (sectionOrigins != null)
    {
        foreach (var item in sectionOrigins)
        {
            var normalized = NormalizeCorsOrigin(item);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                origins.Add(normalized);
            }
        }
    }

    var envOrigins = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS")
        ?? configuration["Cors:AllowedOrigins"];
    if (!string.IsNullOrWhiteSpace(envOrigins))
    {
        foreach (var item in envOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = NormalizeCorsOrigin(item);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                origins.Add(normalized);
            }
        }
    }

    var frontendBaseUrl = NormalizeCorsOrigin(configuration["Frontend:BaseUrl"]);
    if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
    {
        origins.Add(frontendBaseUrl);
    }

    if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
    {
        origins.Add("http://localhost:5173");
        origins.Add("https://localhost:5173");
        origins.Add("http://127.0.0.1:5173");
        origins.Add("https://127.0.0.1:5173");
        origins.Add("http://localhost:5062");
        origins.Add("https://localhost:7009");
    }

    return origins;
}

static bool IsAllowedCorsOrigin(string origin, HashSet<string> allowedOrigins, IHostEnvironment environment)
{
    var normalized = NormalizeCorsOrigin(origin);
    if (string.IsNullOrWhiteSpace(normalized))
    {
        return false;
    }

    if (allowedOrigins.Contains(normalized) ||
        string.Equals(normalized, "https://accounts.google.com", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if ((environment.IsDevelopment() || environment.IsEnvironment("Testing")) &&
        Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
    {
        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }

    return false;
}

static string? NormalizeCorsOrigin(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    return Uri.TryCreate(value.Trim().TrimEnd('/'), UriKind.Absolute, out var uri)
        ? $"{uri.Scheme}://{uri.Authority}"
        : null;
}

public partial class Program;
