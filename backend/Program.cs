using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
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

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<AccountingCrudService>();
builder.Services.AddScoped<PayrollBatchCalculationService>();

builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IVehicleService, VehicleService>();

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddScoped<IServiceOrderRepository, ServiceOrderRepository>();
builder.Services.AddScoped<IServiceOrderService, ServiceOrderService>();

builder.Services.AddScoped<IServiceOrderDetailRepository, ServiceOrderDetailRepository>();
builder.Services.AddScoped<IServiceOrderDetailService, ServiceOrderDetailService>();

builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IAppointmentStatusPolicy, AppointmentStatusPolicy>();
builder.Services.AddScoped<IAppointmentDetailAssembler, AppointmentDetailAssembler>();
builder.Services.AddScoped<IBookingGroupStatusPolicy, BookingGroupStatusPolicy>();
builder.Services.AddScoped<IBookingGroupService, BookingGroupService>();
builder.Services.AddHostedService<AppointmentDispatchBackgroundService>();

builder.Services.AddScoped<IStaffRepository, StaffRepository>();
builder.Services.AddScoped<IStaffService, StaffService>();

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

builder.Services.AddScoped<IAiContentStoreRepository, AiContentStoreRepository>();
builder.Services.AddScoped<IAiContentStoreService, AiContentStoreService>();

builder.Services.AddScoped<IAiCustomerInsightRepository, AiCustomerInsightRepository>();
builder.Services.AddScoped<IAiCustomerInsightService, AiCustomerInsightService>();

builder.Services.AddScoped<IAiForecastRepository, AiForecastRepository>();
builder.Services.AddScoped<IAiForecastService, AiForecastService>();

builder.Services.AddScoped<IAiFraudMonitorRepository, AiFraudMonitorRepository>();
builder.Services.AddScoped<IAiFraudMonitorService, AiFraudMonitorService>();

builder.Services.AddScoped<IAiKnowledgeBaseRepository, AiKnowledgeBaseRepository>();
builder.Services.AddScoped<IAiKnowledgeBaseService, AiKnowledgeBaseService>();

builder.Services.AddScoped<IAiLearningFeedbackRepository, AiLearningFeedbackRepository>();
builder.Services.AddScoped<IAiLearningFeedbackService, AiLearningFeedbackService>();

builder.Services.AddScoped<IAiModelConfigRepository, AiModelConfigRepository>();
builder.Services.AddScoped<IAiModelConfigService, AiModelConfigService>();

builder.Services.AddScoped<IAiSmartDispatchingRepository, AiSmartDispatchingRepository>();
builder.Services.AddScoped<IAiSmartDispatchingService, AiSmartDispatchingService>();

builder.Services.AddScoped<IAiTechnicalAssistantRepository, AiTechnicalAssistantRepository>();
builder.Services.AddScoped<IAiTechnicalAssistantService, AiTechnicalAssistantService>();

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

builder.Services.AddScoped<IDiagnoseRecommendationRepository, DiagnoseRecommendationRepository>();
builder.Services.AddScoped<IDiagnoseRecommendationService, DiagnoseRecommendationService>();

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

builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();

builder.Services.AddScoped<IWorkSlotRepository, WorkSlotRepository>();
builder.Services.AddScoped<IWorkSlotService, WorkSlotService>();

// Authentication & JWT Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<PaymentConfirmationService>();
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
    return new PayOSClient(new PayOSOptions
    {
        ClientId = config["PayOS:ClientId"] ?? Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID"),
        ApiKey = config["PayOS:ApiKey"] ?? Environment.GetEnvironmentVariable("PAYOS_API_KEY"),
        ChecksumKey = config["PayOS:ChecksumKey"] ?? Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY"),
        LogLevel = LogLevel.Debug,
    });
});

builder.Services.AddKeyedSingleton("TransferClient", (sp, key) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new PayOSClient(new PayOSOptions
    {
        ClientId = config["PayOS:PayoutClientId"],
        ApiKey = config["PayOS:PayoutApiKey"],
        ChecksumKey = config["PayOS:PayoutChecksumKey"],
        LogLevel = LogLevel.Debug,
    });
});

// Add services to the container.

builder.Services.AddControllers();
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

app.UseHttpsRedirection();
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
