using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Repositories;
using MyGarage.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using PayOS;
using System.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "FrontendCorsPolicy";

if (builder.Environment.IsDevelopment())
{
    // Keep local development stable on the requested ports.
    builder.WebHost.UseUrls("https://localhost:7009");
}

builder.Services.AddDbContext<GarageContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GarageDbConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.SetIsOriginAllowed(origin =>
                origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("https://localhost:", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("https://127.0.0.1:", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(origin, "https://accounts.google.com", StringComparison.OrdinalIgnoreCase))
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped(typeof(IGenericService<>), typeof(GenericService<>));
builder.Services.AddScoped<AccountingCrudService>();

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
builder.Services.AddHostedService<AppointmentNoShowService>();
builder.Services.AddHostedService<AppointmentReminderService>();
builder.Services.AddHostedService<VoucherCampaignLifecycleService>();

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
builder.Services.AddScoped<PaymentConfirmationService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

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
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});
builder.Services.AddAuthorization();

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
await EnsureDefaultBranchesAsync(app);
await EnsureCustomerPreferredBranchTagsAsync(app);
await EnsureAppointmentSchemaCompatibilityAsync(app);
await EnsureSalaryCoefficientSchemaCompatibilityAsync(app);
await EnsureAdminDemoDataAsync(app);
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
else
{
    app.MapGet("/", () => Results.Content(
        "Frontend build not found. For local development run frontend with 'npm run dev' at https://localhost:5173, or run 'npm run build' in /frontend so backend can serve /frontend/dist.",
        "text/plain"));
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

/// <summary>
/// Checks if the given table has active (non-disabled) triggers in SQL Server.
/// Used to skip seeding/schema operations silently when security triggers would block them.
/// </summary>
static async Task<bool> HasActiveTriggersAsync(GarageContext db, string tableName)
{
    try
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(1) FROM sys.triggers
WHERE parent_id = OBJECT_ID(@tableName)
  AND is_disabled = 0";

            var param = command.CreateParameter();
            param.ParameterName = "@tableName";
            param.Value = $"dbo.{tableName}";
            command.Parameters.Add(param);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result ?? 0) > 0;
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }
    catch
    {
        return false; // If we can't check, assume no triggers and let normal error handling work.
    }
}

/// <summary>
/// Checks for active database-level DDL triggers.
/// Used to avoid runtime schema changes when security policies block ALTER statements.
/// </summary>
static async Task<bool> HasDatabaseDdlTriggersAsync(GarageContext db)
{
    try
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(1)
FROM sys.triggers
WHERE parent_class_desc = 'DATABASE'
  AND is_disabled = 0";

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result ?? 0) > 0;
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }
    catch
    {
        // Be conservative for DDL operations: if metadata check fails, do not attempt ALTER TABLE.
        return true;
    }
}

static async Task EnsureAppointmentSchemaCompatibilityAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GarageContext>();

    // Check for DDL-blocking triggers on Appointments table first.
    // If active triggers exist, skip schema changes silently — they will be blocked anyway.
    var hasAppointmentTriggers = await HasActiveTriggersAsync(db, "Appointments");
    var hasDatabaseDdlTriggers = await HasDatabaseDdlTriggersAsync(db);
    if (hasAppointmentTriggers || hasDatabaseDdlTriggers)
    {
        app.Logger.LogDebug("Skipping Appointments schema fix: active security triggers may block DDL changes.");
        return;
    }

    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.Appointments', N'U') IS NULL
    RETURN;

IF COL_LENGTH(N'dbo.Appointments', N'ScheduledDate') IS NULL
    ALTER TABLE dbo.Appointments ADD ScheduledDate date NULL;

IF COL_LENGTH(N'dbo.Appointments', N'StartTime') IS NULL
    ALTER TABLE dbo.Appointments ADD StartTime time(0) NULL;

IF COL_LENGTH(N'dbo.Appointments', N'EndTime') IS NULL
    ALTER TABLE dbo.Appointments ADD EndTime time(0) NULL;");

        await db.Database.ExecuteSqlRawAsync(@"
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.Appointments', N'U') IS NULL
    RETURN;

BEGIN TRAN;

DECLARE @dcName sysname;
DECLARE @scheduledType nvarchar(128);
DECLARE @startType nvarchar(128);
DECLARE @endType nvarchar(128);

SELECT @scheduledType = DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'ScheduledDate';

SELECT @startType = DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'StartTime';

SELECT @endType = DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'EndTime';

IF LOWER(ISNULL(@scheduledType, '')) <> 'date'
BEGIN
    UPDATE dbo.Appointments
    SET ScheduledDate = TRY_CONVERT(date, ScheduledDate)
    WHERE TRY_CONVERT(date, ScheduledDate) IS NOT NULL;

    SELECT @dcName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Appointments')
      AND c.name = N'ScheduledDate';

    IF @dcName IS NOT NULL
        EXEC(N'ALTER TABLE dbo.Appointments DROP CONSTRAINT [' + @dcName + N']');

    ALTER TABLE dbo.Appointments ALTER COLUMN ScheduledDate date NULL;
END;

IF LOWER(ISNULL(@startType, '')) <> 'time'
BEGIN
    UPDATE dbo.Appointments
    SET StartTime = TRY_CONVERT(time(0), StartTime)
    WHERE TRY_CONVERT(time(0), StartTime) IS NOT NULL;

    SELECT @dcName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Appointments')
      AND c.name = N'StartTime';

    IF @dcName IS NOT NULL
        EXEC(N'ALTER TABLE dbo.Appointments DROP CONSTRAINT [' + @dcName + N']');

    ALTER TABLE dbo.Appointments ALTER COLUMN StartTime time(0) NULL;
END;

IF LOWER(ISNULL(@endType, '')) <> 'time'
BEGIN
    UPDATE dbo.Appointments
    SET EndTime = TRY_CONVERT(time(0), EndTime)
    WHERE TRY_CONVERT(time(0), EndTime) IS NOT NULL;

    SELECT @dcName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Appointments')
      AND c.name = N'EndTime';

    IF @dcName IS NOT NULL
        EXEC(N'ALTER TABLE dbo.Appointments DROP CONSTRAINT [' + @dcName + N']');

    ALTER TABLE dbo.Appointments ALTER COLUMN EndTime time(0) NULL;
END;

COMMIT TRAN;");

        await db.Database.ExecuteSqlRawAsync(@"
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.Appointments', N'U') IS NULL
    RETURN;

BEGIN TRAN;

UPDATE dbo.Appointments
SET ScheduledDate = COALESCE(
    TRY_CONVERT(date, ScheduledDate),
    TRY_CONVERT(date, CreatedAt),
    CONVERT(date, GETDATE())
)
WHERE ScheduledDate IS NULL;

UPDATE dbo.Appointments
SET StartTime = COALESCE(TRY_CONVERT(time(0), StartTime), CAST('08:00:00' AS time(0)))
WHERE StartTime IS NULL;

UPDATE dbo.Appointments
SET EndTime = COALESCE(
    TRY_CONVERT(time(0), EndTime),
    TRY_CONVERT(time(0), DATEADD(HOUR, 1, CAST(StartTime AS datetime2))),
    CAST('09:00:00' AS time(0))
)
WHERE EndTime IS NULL;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Appointments')
      AND name = N'ScheduledDate'
      AND is_nullable = 1
)
    ALTER TABLE dbo.Appointments ALTER COLUMN ScheduledDate date NOT NULL;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Appointments')
      AND name = N'StartTime'
      AND is_nullable = 1
)
    ALTER TABLE dbo.Appointments ALTER COLUMN StartTime time(0) NOT NULL;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Appointments')
      AND name = N'EndTime'
      AND is_nullable = 1
)
    ALTER TABLE dbo.Appointments ALTER COLUMN EndTime time(0) NOT NULL;

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Appointments')
      AND c.name = N'ScheduledDate'
)
    ALTER TABLE dbo.Appointments
        ADD CONSTRAINT DF_Appointments_ScheduledDate
        DEFAULT (CONVERT(date, GETDATE())) FOR ScheduledDate;

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Appointments')
      AND c.name = N'StartTime'
)
    ALTER TABLE dbo.Appointments
        ADD CONSTRAINT DF_Appointments_StartTime
        DEFAULT ('08:00:00') FOR StartTime;

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Appointments')
      AND c.name = N'EndTime'
)
    ALTER TABLE dbo.Appointments
        ADD CONSTRAINT DF_Appointments_EndTime
        DEFAULT ('09:00:00') FOR EndTime;

COMMIT TRAN;");

        app.Logger.LogInformation("Ensured Appointments schema compatibility (ScheduledDate/StartTime/EndTime).");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not auto-fix Appointments schema. The app will continue with current schema.");
    }
}

static async Task EnsureSalaryCoefficientSchemaCompatibilityAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GarageContext>();

    var hasDatabaseDdlTriggers = await HasDatabaseDdlTriggersAsync(db);
    if (hasDatabaseDdlTriggers)
    {
        app.Logger.LogDebug("Skipping SalaryCoefficients schema setup: active security triggers may block DDL changes.");
        return;
    }

    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.SalaryCoefficients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalaryCoefficients
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalaryCoefficients PRIMARY KEY,
        YearsOfService INT NULL,
        CoefficientName NVARCHAR(120) NOT NULL CONSTRAINT DF_SalaryCoefficients_CoefficientName DEFAULT (N''),
        CoefficientValue DECIMAL(8,3) NOT NULL,
        Description NVARCHAR(500) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_SalaryCoefficients_IsActive DEFAULT (1),
        CreatedAt DATETIME NULL CONSTRAINT DF_SalaryCoefficients_CreatedAt DEFAULT (GETDATE()),
        CreatedBy BIGINT NULL,
        UpdatedAt DATETIME NULL CONSTRAINT DF_SalaryCoefficients_UpdatedAt DEFAULT (GETDATE()),
        UpdatedBy BIGINT NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SalaryCoefficients_IsDeleted DEFAULT (0),
        DeletedAt DATETIME NULL,
        DeletedBy BIGINT NULL
    );
END;

IF COL_LENGTH(N'dbo.SalaryCoefficients', N'YearsOfService') IS NULL
BEGIN
    ALTER TABLE dbo.SalaryCoefficients
        ADD YearsOfService INT NULL;
END;

IF COL_LENGTH(N'dbo.SalaryCoefficients', N'CoefficientName') IS NULL
BEGIN
    ALTER TABLE dbo.SalaryCoefficients
        ADD CoefficientName NVARCHAR(120) NULL;
END;

IF COL_LENGTH(N'dbo.SalaryCoefficients', N'CoefficientValue') IS NULL
BEGIN
    ALTER TABLE dbo.SalaryCoefficients
        ADD CoefficientValue DECIMAL(8,3) NULL;
END;

IF COL_LENGTH(N'dbo.SalaryCoefficients', N'IsActive') IS NULL
BEGIN
    ALTER TABLE dbo.SalaryCoefficients
        ADD IsActive BIT NULL;
END;

IF COL_LENGTH(N'dbo.SalaryCoefficients', N'IsDeleted') IS NULL
BEGIN
    ALTER TABLE dbo.SalaryCoefficients
        ADD IsDeleted BIT NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.SalaryCoefficients')
      AND c.name = N'CoefficientName'
)
BEGIN
    ALTER TABLE dbo.SalaryCoefficients
        ADD CONSTRAINT DF_SalaryCoefficients_CoefficientName DEFAULT (N'') FOR CoefficientName;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.SalaryCoefficients')
      AND c.name = N'IsActive'
)
BEGIN
    ALTER TABLE dbo.SalaryCoefficients
        ADD CONSTRAINT DF_SalaryCoefficients_IsActive DEFAULT (1) FOR IsActive;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.SalaryCoefficients')
      AND c.name = N'IsDeleted'
)
BEGIN
    ALTER TABLE dbo.SalaryCoefficients
        ADD CONSTRAINT DF_SalaryCoefficients_IsDeleted DEFAULT (0) FOR IsDeleted;
END;

IF COL_LENGTH(N'dbo.SalaryCoefficients', N'IsActive') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE dbo.SalaryCoefficients
        SET IsActive = 1
        WHERE IsActive IS NULL;
    ');
END;

IF COL_LENGTH(N'dbo.SalaryCoefficients', N'IsDeleted') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE dbo.SalaryCoefficients
        SET IsDeleted = 0
        WHERE IsDeleted IS NULL;
    ');
END;

IF COL_LENGTH(N'dbo.SalaryCoefficients', N'CoefficientValue') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE dbo.SalaryCoefficients
        SET CoefficientValue = 0
        WHERE CoefficientValue IS NULL;
    ');
END;

IF COL_LENGTH(N'dbo.SalaryCoefficients', N'YearsOfService') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE dbo.SalaryCoefficients
        SET CoefficientName = CONCAT(CAST(YearsOfService AS NVARCHAR(20)), N'' years'')
        WHERE YearsOfService IS NOT NULL
          AND (CoefficientName IS NULL OR LTRIM(RTRIM(CoefficientName)) = N'''');
    ');
END;

IF COL_LENGTH(N'dbo.SalaryCoefficients', N'YearsOfService') IS NOT NULL
   AND OBJECT_ID(N'dbo.CK_SalaryCoefficients_YearsOfService_NonNegative', N'C') IS NULL
BEGIN
    EXEC(N'
        ALTER TABLE dbo.SalaryCoefficients WITH NOCHECK
        ADD CONSTRAINT CK_SalaryCoefficients_YearsOfService_NonNegative
        CHECK (YearsOfService IS NULL OR YearsOfService >= 0);
    ');
END;

IF COL_LENGTH(N'dbo.SalaryCoefficients', N'CoefficientValue') IS NOT NULL
   AND OBJECT_ID(N'dbo.CK_SalaryCoefficients_CoefficientValue_NonNegative', N'C') IS NULL
BEGIN
    EXEC(N'
        ALTER TABLE dbo.SalaryCoefficients WITH NOCHECK
        ADD CONSTRAINT CK_SalaryCoefficients_CoefficientValue_NonNegative
        CHECK (CoefficientValue >= 0);
    ');
END;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SalaryCoefficients')
      AND name = N'UX_SalaryCoefficients_CoefficientName'
)
BEGIN
    DROP INDEX UX_SalaryCoefficients_CoefficientName ON dbo.SalaryCoefficients;
END;

IF COL_LENGTH(N'dbo.SalaryCoefficients', N'YearsOfService') IS NOT NULL
   AND COL_LENGTH(N'dbo.SalaryCoefficients', N'IsDeleted') IS NOT NULL
   AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SalaryCoefficients')
      AND name = N'UX_SalaryCoefficients_YearsOfService_Active'
)
BEGIN
    DECLARE @HasDuplicateYears BIT = 0;

    EXEC sp_executesql
        N'
            SELECT @HasDuplicateYears = CASE WHEN EXISTS (
                SELECT 1
                FROM dbo.SalaryCoefficients
                WHERE YearsOfService IS NOT NULL
                  AND ISNULL(IsDeleted, 0) = 0
                GROUP BY YearsOfService
                HAVING COUNT(1) > 1
            ) THEN 1 ELSE 0 END;
        ',
        N'@HasDuplicateYears BIT OUTPUT',
        @HasDuplicateYears OUTPUT;

    IF @HasDuplicateYears = 1
    BEGIN
        PRINT N'Skipped UX_SalaryCoefficients_YearsOfService_Active creation due to duplicate active YearsOfService values.';
    END;
    ELSE
    BEGIN
        EXEC(N'
            CREATE UNIQUE INDEX UX_SalaryCoefficients_YearsOfService_Active
                ON dbo.SalaryCoefficients (YearsOfService)
                WHERE YearsOfService IS NOT NULL AND IsDeleted = 0;
        ');
    END;
END;");

        app.Logger.LogInformation("Ensured SalaryCoefficients schema compatibility.");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not auto-create SalaryCoefficients schema. You may need to run SQL script manually.");
    }
}

static async Task EnsureDefaultBranchesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
    var now = DateTime.UtcNow;

    static string RemoveDiacritics(string? value)
    {
        var text = value ?? string.Empty;
        var normalized = text.Normalize(NormalizationForm.FormD);
        var chars = new List<char>(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                chars.Add(ch);
            }
        }

        return new string(chars.ToArray())
            .Normalize(NormalizationForm.FormC)
            .Replace('\u0111', 'd')
            .Replace('\u0110', 'D');
    }

    static string NormalizeBranchNameKey(string? value)
    {
        var normalized = RemoveDiacritics(value)
            .ToLowerInvariant()
            .Replace('.', ' ')
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Trim();

        normalized = string.Join(" ", normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return normalized switch
        {
            "ha noi" or "hanoi" => "ha noi",
            "da nang" or "danang" => "da nang",
            "hcm" or "tp hcm" or "tphcm" or "ho chi minh" or "sai gon" or "saigon" => "hcm",
            _ => normalized
        };
    }

    static int BranchScore(Branch branch, string canonicalName)
    {
        var score = 0;
        var branchName = branch.Name?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(branchName) && RemoveDiacritics(branchName) != branchName)
        {
            score += 2;
        }

        if (string.Equals(branchName, canonicalName, StringComparison.OrdinalIgnoreCase))
        {
            score += 3;
        }

        if (string.Equals(branch.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
        }

        return score;
    }

    var defaults = new[]
    {
        new
        {
            Name = "H\u00E0 N\u1ED9i",
            Address = "H\u00E0 N\u1ED9i",
            Hotline = "1900-0001",
            Lat = 21.0278m,
            Lng = 105.8342m,
            LegacyNames = new[] { "Chi nhánh Bắc", "Bac", "Hanoi", "Ha Noi", "Bắc" }
        },
        new
        {
            Name = "\u0110\u00E0 N\u1EB5ng",
            Address = "\u0110\u00E0 N\u1EB5ng",
            Hotline = "1900-0002",
            Lat = 16.0544m,
            Lng = 108.2022m,
            LegacyNames = new[] { "Chi nhánh Trung", "Trung", "DaNang", "Da Nang", "Đà Nẵng" }
        },
        new
        {
            Name = "HCM",
            Address = "TP HCM",
            Hotline = "1900-0003",
            Lat = 10.8231m,
            Lng = 106.6297m,
            LegacyNames = new[] { "Chi nhánh Nam", "Nam", "Sai Gon", "Ho Chi Minh", "TP.HCM", "TP HCM" }
        }
    };

    var existingBranches = await db.Branches
        .Where(b => b.IsDeleted != true && b.Name != null)
        .ToListAsync();

    foreach (var item in defaults)
    {
        var knownKeys = item.LegacyNames
            .Append(item.Name)
            .Select(NormalizeBranchNameKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        var existing = existingBranches
            .Where(b => b.Name != null && knownKeys.Contains(NormalizeBranchNameKey(b.Name)))
            .OrderByDescending(b => BranchScore(b, item.Name))
            .ThenBy(b => b.Id)
            .FirstOrDefault();

        if (existing is null)
        {
            var branch = new Branch
            {
                Name = item.Name,
                Address = item.Address,
                Hotline = item.Hotline,
                Lat = item.Lat,
                Lng = item.Lng,
                Status = "active",
                MaxWorkSlots = 20,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Branches.Add(branch);
            existingBranches.Add(branch);
            continue;
        }

        existing.Name = item.Name;
        existing.Address = item.Address;
        existing.Status = "active";
        existing.IsDeleted = false;
        existing.DeletedAt = null;
        existing.DeletedBy = null;
        existing.UpdatedAt = now;

        if (!existing.Lat.HasValue || !existing.Lng.HasValue)
        {
            existing.Lat = item.Lat;
            existing.Lng = item.Lng;
        }

        if (string.IsNullOrWhiteSpace(existing.Hotline))
        {
            existing.Hotline = item.Hotline;
        }
    }

    await db.SaveChangesAsync();

    // Soft-delete legacy duplicates (e.g. "Da Nang" vs "Đà Nẵng") so dropdowns never show duplicates again.
    var activeBranches = await db.Branches
        .Where(b => b.IsDeleted != true && b.Name != null)
        .OrderBy(b => b.Id)
        .ToListAsync();

    var hasDuplicateChanges = false;

    foreach (var item in defaults)
    {
        var canonicalKey = NormalizeBranchNameKey(item.Name);
        var candidates = activeBranches
            .Where(b => NormalizeBranchNameKey(b.Name) == canonicalKey)
            .ToList();

        if (candidates.Count <= 1)
        {
            continue;
        }

        var keeper = candidates
            .OrderByDescending(b => BranchScore(b, item.Name))
            .ThenBy(b => b.Id)
            .First();

        foreach (var duplicate in candidates.Where(b => b.Id != keeper.Id))
        {
            duplicate.IsDeleted = true;
            duplicate.DeletedAt ??= now;
            duplicate.UpdatedAt = now;
            hasDuplicateChanges = true;

            app.Logger.LogInformation(
                "Soft-deleted duplicate branch #{DuplicateId} ({DuplicateName}); kept #{KeepId} ({KeepName}).",
                duplicate.Id,
                duplicate.Name,
                keeper.Id,
                keeper.Name);
        }
    }

    if (hasDuplicateChanges)
    {
        await db.SaveChangesAsync();
    }
}

static async Task EnsureCustomerPreferredBranchTagsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GarageContext>();

    var defaultBranchId = await db.Branches
        .Where(b => b.IsDeleted != true)
        .OrderBy(b => b.Id)
        .Select(b => (long?)b.Id)
        .FirstOrDefaultAsync();

    if (!defaultBranchId.HasValue)
    {
        return;
    }

    var customers = await db.Customers
        .Where(c => c.IsDeleted != true)
        .ToListAsync();

    var changed = false;
    foreach (var customer in customers)
    {
        if (HasPreferredBranchTag(customer.Note))
        {
            continue;
        }

        var tag = $"[PreferredBranchId:{defaultBranchId.Value}]";
        customer.Note = string.IsNullOrWhiteSpace(customer.Note)
            ? tag
            : $"{customer.Note} | {tag}";
        customer.UpdatedAt = DateTime.UtcNow;
        changed = true;
    }

    if (changed)
    {
        await db.SaveChangesAsync();
    }
}

static bool HasPreferredBranchTag(string? note)
{
    if (string.IsNullOrWhiteSpace(note)) return false;
    return note.IndexOf("[PreferredBranchId:", StringComparison.OrdinalIgnoreCase) >= 0;
}

static async Task EnsureAdminDemoDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GarageContext>();

    async Task<bool> HasColumnAsync(string tableName, string columnName)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(1)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @tableName
  AND COLUMN_NAME = @columnName";

            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@tableName";
            tableParam.Value = tableName;
            command.Parameters.Add(tableParam);

            var columnParam = command.CreateParameter();
            columnParam.ParameterName = "@columnName";
            columnParam.Value = columnName;
            command.Parameters.Add(columnParam);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result ?? 0) > 0;
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }

    // Check which tables have DML-blocking triggers so we can skip seeding silently
    var serviceCategoriesLocked = await HasActiveTriggersAsync(db, "ServiceCategories");
    var standardServicesLocked = await HasActiveTriggersAsync(db, "StandardServices");

    var now = DateTime.UtcNow;

    // Roles
    var roleSpecs = new[]
    {
        new { Code = "ADMIN", Name = "Admin" },
        new { Code = "RECEPTIONIST", Name = "Service Advisor" },
        new { Code = "TECH", Name = "Technician" }
    };
    var roles = await db.Roles.Where(r => r.RoleCode != null).ToListAsync();
    var roleByCode = roles.ToDictionary(
        r => r.RoleCode!.Trim().ToUpperInvariant(),
        r => r,
        StringComparer.OrdinalIgnoreCase);

    foreach (var spec in roleSpecs)
    {
        if (roleByCode.ContainsKey(spec.Code))
        {
            continue;
        }

        var role = new Role
        {
            RoleCode = spec.Code,
            RoleName = spec.Name,
            Scope = spec.Code == "ADMIN" ? "COMPANY" : "BRANCH",
            Description = $"Demo role {spec.Name}",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Roles.Add(role);
        roleByCode[spec.Code] = role;
    }

    if (db.ChangeTracker.HasChanges())
    {
        await db.SaveChangesAsync();
    }

    var adminRole = roleByCode["ADMIN"];
    var advisorRole = roleByCode["RECEPTIONIST"];
    var techRole = roleByCode["TECH"];

    // Branch references
    var branchCandidates = await db.Branches
        .Where(b => b.IsDeleted != true && b.Name != null)
        .ToListAsync();

    Branch FindBranchByNames(params string[] names)
    {
        var normalized = names.Select(x => x.Trim().ToLowerInvariant()).ToHashSet();
        var branch = branchCandidates.FirstOrDefault(
            b => b.Name != null && normalized.Contains(b.Name.Trim().ToLowerInvariant()));
        return branch ?? throw new InvalidOperationException($"Missing branch: {string.Join(", ", names)}");
    }

    var northBranch = FindBranchByNames("H\u00E0 N\u1ED9i", "Ha Noi", "Hanoi", "Chi nhánh Bắc");
    var centralBranch = FindBranchByNames("\u0110\u00E0 N\u1EB5ng", "Da Nang", "Chi nhánh Trung");
    var southBranch = FindBranchByNames("HCM", "TP HCM", "Chi nhánh Nam");
    var branches = new[] { northBranch, centralBranch, southBranch };
    var branchIds = branches.Select(b => b.Id).Distinct().ToList();

    // BranchOperatingHours (Mon-Sat 7:30-17:30, Sun off)
    var existingHourKeys = await db.BranchOperatingHours
        .Where(h => branchIds.Contains(h.BranchId))
        .Select(h => new { h.BranchId, h.DayOfWeek })
        .ToListAsync();
    var branchHourSet = existingHourKeys
        .Select(h => (h.BranchId, h.DayOfWeek))
        .ToHashSet();

    foreach (var branch in branches)
    {
        for (var dow = 0; dow <= 6; dow++)
        {
            if (branchHourSet.Contains((branch.Id, dow)))
            {
                continue;
            }

            db.BranchOperatingHours.Add(new BranchOperatingHour
            {
                BranchId = branch.Id,
                DayOfWeek = dow,
                OpenTime = new TimeOnly(7, 30),
                CloseTime = new TimeOnly(17, 30),
                IsDayOff = dow == 0,
                SlotDurationMinutes = 60,
                MaxConcurrentBookings = 10,
                UpdatedAt = now
            });
        }
    }

    if (db.ChangeTracker.HasChanges())
    {
        await db.SaveChangesAsync();
    }

    // Customer tiers
    var tierSpecs = new[]
    {
        new { Name = "Bronze", MinSpending = 0m, DiscountPercent = 0m },
        new { Name = "Silver", MinSpending = 5000000m, DiscountPercent = 3m },
        new { Name = "Gold", MinSpending = 15000000m, DiscountPercent = 7m }
    };
    var tierNameSet = (await db.CustomerTiers
        .Where(t => t.TierName != null)
        .Select(t => t.TierName!.ToLower())
        .ToListAsync())
        .ToHashSet(StringComparer.Ordinal);

    foreach (var spec in tierSpecs)
    {
        var loweredName = spec.Name.Trim().ToLowerInvariant();
        if (tierNameSet.Contains(loweredName))
        {
            continue;
        }

        db.CustomerTiers.Add(new CustomerTier
        {
            TierName = spec.Name,
            MinSpending = spec.MinSpending,
            DiscountPercent = spec.DiscountPercent,
            Benefits = $"Demo tier {spec.Name}",
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        tierNameSet.Add(loweredName);
    }

    if (db.ChangeTracker.HasChanges())
    {
        await db.SaveChangesAsync();
    }

    // Service categories
    if (serviceCategoriesLocked)
    {
        app.Logger.LogInformation("Skipping ServiceCategories seeding: table has active security triggers.");
    }
    else
    {
        var categorySpecs = new[]
        {
            new { Code = "MAINT",  Name = "Bảo dưỡng", Description = "Dịch vụ bảo dưỡng định kỳ" },
            new { Code = "REPAIR", Name = "Sửa chữa",  Description = "Dịch vụ sửa chữa" },
            new { Code = "TIRES",  Name = "Lốp & Phanh", Description = "Dịch vụ lốp và phanh" }
        };

        var existingCodes = (await db.ServiceCategories
            .Where(c => c.Code != null)
            .Select(c => c.Code!)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in categorySpecs)
        {
            if (existingCodes.Contains(spec.Code))
                continue;

            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO dbo.ServiceCategories (Code, Name, Description, IsActive, IsDeleted, CreatedAt, UpdatedAt)
                      VALUES ({0}, {1}, {2}, 1, 0, {3}, {4})",
                    spec.Code, spec.Name, spec.Description, now, now);
                app.Logger.LogInformation("Seeded ServiceCategory: {Code}", spec.Code);
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Could not seed ServiceCategory {Code}.", spec.Code);
            }
        }
    }

    // Re-fetch all categories (including newly inserted) for the rest of the seeder.
    var categoryByCode = (await db.ServiceCategories
        .Where(c => c.Code != null)
        .ToListAsync())
        .ToDictionary(c => c.Code!, c => c, StringComparer.OrdinalIgnoreCase);


    // Guard: if DB trigger blocked the inserts or categories missing, skip standard service seeding.
    if (standardServicesLocked ||
        !categoryByCode.TryGetValue("MAINT",  out var catMaint)  ||
        !categoryByCode.TryGetValue("REPAIR", out var catRepair) ||
        !categoryByCode.TryGetValue("TIRES",  out var catTire))
    {
        if (standardServicesLocked)
            app.Logger.LogInformation("Skipping StandardServices seeding: table has active security triggers.");
        else
            app.Logger.LogInformation("Skipping StandardServices seeding: required ServiceCategories not found.");
    }
    else
    {
        // Standard services
        var standardServiceSpecs = new[]
        {
            new { Code = "MAINT-OC",  Name = "Thay nhớt nhanh",          CategoryId = catMaint.Id,  BasePrice = 280000m, StandardMinutes = 40 },
            new { Code = "REP-ENG",   Name = "Chẩn đoán động cơ",         CategoryId = catRepair.Id, BasePrice = 450000m, StandardMinutes = 60 },
            new { Code = "TIRE-BAL",  Name = "Cân bằng động bánh xe",     CategoryId = catTire.Id,   BasePrice = 220000m, StandardMinutes = 30 }
        };
        var serviceCodeSet = (await db.StandardServices
            .Where(s => s.Code != null)
            .Select(s => s.Code!)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in standardServiceSpecs)
        {
            if (serviceCodeSet.Contains(spec.Code))
                continue;

            db.StandardServices.Add(new StandardService
            {
                Code = spec.Code,
                Name = spec.Name,
                CategoryId = spec.CategoryId,
                BasePrice = spec.BasePrice,
                StandardMinutes = spec.StandardMinutes,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (db.ChangeTracker.HasChanges())
        {
            try { await db.SaveChangesAsync(); }
            catch (Exception ex) { app.Logger.LogWarning(ex, "Could not seed StandardServices. Non-fatal."); }
        }
    }

    // Vehicle brands + models
    var brandSpecs = new[]
    {
        new { Code = "TOY", Name = "Toyota", Country = "Japan" },
        new { Code = "HON", Name = "Honda", Country = "Japan" },
        new { Code = "FOR", Name = "Ford", Country = "USA" }
    };
    var brands = await db.VehicleBrands
        .Where(b => b.Code != null)
        .ToListAsync();
    var brandByCode = brands.ToDictionary(
        b => b.Code!,
        b => b,
        StringComparer.OrdinalIgnoreCase);

    foreach (var spec in brandSpecs)
    {
        if (brandByCode.ContainsKey(spec.Code))
        {
            continue;
        }

        var brand = new VehicleBrand
        {
            Code = spec.Code,
            Name = spec.Name,
            CountryOfOrigin = spec.Country,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.VehicleBrands.Add(brand);
        brandByCode[spec.Code] = brand;
    }

    if (db.ChangeTracker.HasChanges())
    {
        await db.SaveChangesAsync();
    }

    var toyota = brandByCode["TOY"];
    var honda = brandByCode["HON"];
    var ford = brandByCode["FOR"];

    var modelSpecs = new[]
    {
        new { BrandId = toyota.Id, ModelName = "Vios", Segment = "Sedan", FuelType = "Gasoline" },
        new { BrandId = honda.Id, ModelName = "City", Segment = "Sedan", FuelType = "Gasoline" },
        new { BrandId = ford.Id, ModelName = "Ranger", Segment = "Pickup", FuelType = "Diesel" }
    };
    var modelBrandIds = modelSpecs.Select(x => x.BrandId).Distinct().ToList();
    var modelNameSet = modelSpecs.Select(x => x.ModelName).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var models = await db.VehicleModels
        .Where(m => modelBrandIds.Contains(m.BrandId) && m.ModelName != null && modelNameSet.Contains(m.ModelName))
        .ToListAsync();
    var modelKeys = models
        .Select(m => $"{m.BrandId}:{m.ModelName}")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var spec in modelSpecs)
    {
        var modelKey = $"{spec.BrandId}:{spec.ModelName}";
        if (modelKeys.Contains(modelKey))
        {
            continue;
        }

        var model = new VehicleModel
        {
            BrandId = spec.BrandId,
            ModelName = spec.ModelName,
            Segment = spec.Segment,
            FuelType = spec.FuelType,
            CylinderCapacity = "1.5L",
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.VehicleModels.Add(model);
        models.Add(model);
        modelKeys.Add(modelKey);
    }

    if (db.ChangeTracker.HasChanges())
    {
        await db.SaveChangesAsync();
    }

    var vios = models.First(m => m.BrandId == toyota.Id && string.Equals(m.ModelName, "Vios", StringComparison.OrdinalIgnoreCase));
    var city = models.First(m => m.BrandId == honda.Id && string.Equals(m.ModelName, "City", StringComparison.OrdinalIgnoreCase));
    var ranger = models.First(m => m.BrandId == ford.Id && string.Equals(m.ModelName, "Ranger", StringComparison.OrdinalIgnoreCase));
    var demoModels = new[] { vios, city, ranger };

    // Staff demo data per branch
    var demoStaff = new[]
    {
        new { FullName = "Admin Bắc", Phone = "0901000001", Email = "admin.bac@mygarage.local", BranchId = northBranch.Id, RoleId = adminRole.Id },
        new { FullName = "Cố vấn Bắc", Phone = "0901000002", Email = "advisor.bac@mygarage.local", BranchId = northBranch.Id, RoleId = advisorRole.Id },
        new { FullName = "KTV Bắc", Phone = "0901000003", Email = "tech.bac@mygarage.local", BranchId = northBranch.Id, RoleId = techRole.Id },
        new { FullName = "Admin Trung", Phone = "0902000001", Email = "admin.trung@mygarage.local", BranchId = centralBranch.Id, RoleId = adminRole.Id },
        new { FullName = "Cố vấn Trung", Phone = "0902000002", Email = "advisor.trung@mygarage.local", BranchId = centralBranch.Id, RoleId = advisorRole.Id },
        new { FullName = "KTV Trung", Phone = "0902000003", Email = "tech.trung@mygarage.local", BranchId = centralBranch.Id, RoleId = techRole.Id },
        new { FullName = "Admin Nam", Phone = "0903000001", Email = "admin.nam@mygarage.local", BranchId = southBranch.Id, RoleId = adminRole.Id },
        new { FullName = "Cố vấn Nam", Phone = "0903000002", Email = "advisor.nam@mygarage.local", BranchId = southBranch.Id, RoleId = advisorRole.Id },
        new { FullName = "KTV Nam", Phone = "0903000003", Email = "tech.nam@mygarage.local", BranchId = southBranch.Id, RoleId = techRole.Id }
    };

    var demoStaffEmails = demoStaff
        .Select(x => x.Email.Trim().ToLowerInvariant())
        .ToList();
    var existingStaffEmailSet = (await db.Staffs
        .Where(s => s.Email != null && demoStaffEmails.Contains(s.Email.ToLower()))
        .Select(s => s.Email!.ToLower())
        .ToListAsync())
        .ToHashSet(StringComparer.Ordinal);

    foreach (var item in demoStaff)
    {
        var loweredEmail = item.Email.Trim().ToLowerInvariant();
        if (existingStaffEmailSet.Contains(loweredEmail))
        {
            continue;
        }

        db.Staffs.Add(new Staff
        {
            FullName = item.FullName,
            Phone = item.Phone,
            Email = item.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            RoleId = item.RoleId,
            BranchId = item.BranchId,
            Status = "ACTIVE",
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        existingStaffEmailSet.Add(loweredEmail);
    }

    if (db.ChangeTracker.HasChanges())
    {
        await db.SaveChangesAsync();
    }

    var defaultTier = await db.CustomerTiers.OrderBy(t => t.Id).FirstOrDefaultAsync();
    var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

    var demoEntries = new List<(Branch Branch, int J, string CustomerEmail, string CustomerPhone, string CustomerName, string Plate, VehicleModel Model)>();
    for (var i = 0; i < branches.Length; i++)
    {
        var branch = branches[i];
        for (var j = 1; j <= 2; j++)
        {
            var customerEmail = $"customer{j}.b{branch.Id}@mygarage.local";
            var customerPhone = $"091{branch.Id % 10}{j}000{j}";
            var customerName = $"Khách {j} - {branch.Name}";
            var model = demoModels[(i + j - 1) % demoModels.Length];
            var plate = $"{30 + i}{char.ConvertFromUtf32(65 + i)}-{12000 + i * 100 + j}";
            demoEntries.Add((branch, j, customerEmail, customerPhone, customerName, plate, model));
        }
    }

    // Customers
    var demoCustomerEmails = demoEntries
        .Select(x => x.CustomerEmail.Trim().ToLowerInvariant())
        .ToList();
    var customers = await db.Customers
        .Where(c => c.Email != null && demoCustomerEmails.Contains(c.Email.ToLower()))
        .ToListAsync();
    var customerByEmail = customers.ToDictionary(
        c => c.Email!.Trim().ToLowerInvariant(),
        c => c,
        StringComparer.Ordinal);

    foreach (var entry in demoEntries)
    {
        var loweredEmail = entry.CustomerEmail.Trim().ToLowerInvariant();
        if (customerByEmail.ContainsKey(loweredEmail))
        {
            continue;
        }

        var customer = new Customer
        {
            FullName = entry.CustomerName,
            Phone = entry.CustomerPhone,
            Email = entry.CustomerEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            TierId = defaultTier?.Id,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Customers.Add(customer);
        customerByEmail[loweredEmail] = customer;
    }

    if (db.ChangeTracker.HasChanges())
    {
        await db.SaveChangesAsync();
    }

    // Vehicles
    var demoPlates = demoEntries.Select(x => x.Plate).ToList();
    var vehicles = await db.Vehicles
        .Where(v => demoPlates.Contains(v.LicensePlate))
        .ToListAsync();
    var vehicleByPlate = vehicles.ToDictionary(v => v.LicensePlate, v => v, StringComparer.OrdinalIgnoreCase);

    foreach (var entry in demoEntries)
    {
        if (vehicleByPlate.ContainsKey(entry.Plate))
        {
            continue;
        }

        var vehicle = new Vehicle
        {
            LicensePlate = entry.Plate,
            ModelId = entry.Model.Id,
            Color = entry.J % 2 == 0 ? "White" : "Black",
            ManufactureYear = 2020 + entry.J,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Vehicles.Add(vehicle);
        vehicleByPlate[entry.Plate] = vehicle;
    }

    if (db.ChangeTracker.HasChanges())
    {
        await db.SaveChangesAsync();
    }

    // CustomerVehicles + Appointments
    var demoCustomerIds = customerByEmail.Values.Select(c => c.Id).Distinct().ToList();
    var demoVehicleIds = vehicleByPlate.Values.Select(v => v.Id).Distinct().ToList();

    var existingOwnershipSet = (await db.CustomerVehicles
        .Where(cv =>
            demoCustomerIds.Contains(cv.CustomerId) &&
            demoVehicleIds.Contains(cv.VehicleId) &&
            cv.IsDeleted != true)
        .Select(cv => $"{cv.CustomerId}:{cv.VehicleId}")
        .ToListAsync())
        .ToHashSet(StringComparer.Ordinal);

    var canSeedAppointments = await HasColumnAsync("Appointments", "ScheduledDate");
    var existingAppointmentSet = new HashSet<string>(StringComparer.Ordinal);
    if (canSeedAppointments)
    {
        existingAppointmentSet = (await db.Appointments
            .Where(a =>
                branchIds.Contains(a.BranchId) &&
                demoCustomerIds.Contains(a.CustomerId) &&
                a.VehicleId.HasValue &&
                demoVehicleIds.Contains(a.VehicleId.Value) &&
                a.ScheduledDate == tomorrow)
            .Select(a => $"{a.BranchId}:{a.CustomerId}:{a.VehicleId!.Value}")
            .ToListAsync())
            .ToHashSet(StringComparer.Ordinal);
    }
    else
    {
        app.Logger.LogInformation("Skipping demo appointment seeding: column Appointments.ScheduledDate not found.");
    }

    foreach (var entry in demoEntries)
    {
        var customer = customerByEmail[entry.CustomerEmail.Trim().ToLowerInvariant()];
        var vehicle = vehicleByPlate[entry.Plate];

        var ownershipKey = $"{customer.Id}:{vehicle.Id}";
        if (!existingOwnershipSet.Contains(ownershipKey))
        {
            db.CustomerVehicles.Add(new CustomerVehicle
            {
                CustomerId = customer.Id,
                VehicleId = vehicle.Id,
                IsCurrentOwner = true,
                Note = $"Demo ownership for {entry.Branch.Name}",
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            });
            existingOwnershipSet.Add(ownershipKey);
        }

        var appointmentKey = $"{entry.Branch.Id}:{customer.Id}:{vehicle.Id}";
        if (canSeedAppointments && !existingAppointmentSet.Contains(appointmentKey))
        {
            db.Appointments.Add(new Appointment
            {
                BranchId = entry.Branch.Id,
                CustomerId = customer.Id,
                VehicleId = vehicle.Id,
                ScheduledDate = tomorrow,
                StartTime = new TimeOnly(9 + entry.J, 0),
                EndTime = new TimeOnly(10 + entry.J, 0),
                Status = "PENDING",
                CustomerNotes = $"Lịch hẹn demo {entry.Branch.Name}",
                IsReminderSent = false,
                CreatedAt = now,
                UpdatedAt = now
            });
            existingAppointmentSet.Add(appointmentKey);
        }
    }

    if (db.ChangeTracker.HasChanges())
    {
        await db.SaveChangesAsync();
    }
}


