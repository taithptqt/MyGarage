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
using System.Text;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "FrontendCorsPolicy";

if (builder.Environment.IsDevelopment())
{
    // Keep local development stable on the requested ports.
    builder.WebHost.UseUrls("https://localhost:7009", "http://localhost:5062");
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
                string.Equals(origin, "https://accounts.google.com", StringComparison.OrdinalIgnoreCase))
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped(typeof(IGenericService<>), typeof(GenericService<>));

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
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
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
        "Frontend build not found. For local development run frontend with 'npm run dev' at http://localhost:5173, or run 'npm run build' in /frontend so backend can serve /frontend/dist.",
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

static async Task EnsureDefaultBranchesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GarageContext>();

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
            LegacyNames = new[] { "Chi nhánh Trung", "Trung", "DaNang", "Đà Nẵng" }
        },
        new
        {
            Name = "HCM",
            Address = "TP HCM",
            Hotline = "1900-0003",
            Lat = 10.8231m,
            Lng = 106.6297m,
            LegacyNames = new[] { "Chi nhánh Nam", "Nam", "Sai Gon", "Ho Chi Minh", "TP.HCM" }
        }
    };

    foreach (var item in defaults)
    {
        var knownNames = item.LegacyNames
            .Append(item.Name)
            .Select(x => x.Trim().ToLowerInvariant())
            .ToList();

        var existing = await db.Branches.FirstOrDefaultAsync(
            b => b.IsDeleted != true && b.Name != null && knownNames.Contains(b.Name.ToLower()));

        if (existing is null)
        {
            db.Branches.Add(new Branch
            {
                Name = item.Name,
                Address = item.Address,
                Hotline = item.Hotline,
                Lat = item.Lat,
                Lng = item.Lng,
                Status = "active",
                MaxWorkSlots = 20,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            continue;
        }

        existing.Name = item.Name;
        existing.Address = item.Address;

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

    var now = DateTime.UtcNow;

    // Roles
    async Task<Role> EnsureRoleAsync(string roleCode, string roleName)
    {
        var code = roleCode.Trim().ToUpperInvariant();
        var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleCode != null && r.RoleCode.ToUpper() == code);
        if (role is not null) return role;

        role = new Role
        {
            RoleCode = code,
            RoleName = roleName,
            Scope = code == "ADMIN" ? "COMPANY" : "BRANCH",
            Description = $"Demo role {roleName}",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    var adminRole = await EnsureRoleAsync("ADMIN", "Admin");
    var advisorRole = await EnsureRoleAsync("RECEPTIONIST", "Service Advisor");
    var techRole = await EnsureRoleAsync("TECH", "Technician");

    // Branch references
    async Task<Branch> FindBranchByNamesAsync(params string[] names)
    {
        var normalized = names.Select(x => x.Trim().ToLowerInvariant()).ToList();
        var branch = await db.Branches.FirstOrDefaultAsync(
            b => b.IsDeleted != true && b.Name != null && normalized.Contains(b.Name.ToLower()));
        if (branch is null)
        {
            throw new InvalidOperationException($"Missing branch: {string.Join(", ", names)}");
        }
        return branch;
    }

    var northBranch = await FindBranchByNamesAsync("H\u00E0 N\u1ED9i", "Ha Noi", "Hanoi", "Chi nhánh Bắc");
    var centralBranch = await FindBranchByNamesAsync("\u0110\u00E0 N\u1EB5ng", "Da Nang", "Chi nhánh Trung");
    var southBranch = await FindBranchByNamesAsync("HCM", "TP HCM", "Chi nhánh Nam");
    var branches = new[] { northBranch, centralBranch, southBranch };

    // BranchOperatingHours (Mon-Sat 7:30-17:30, Sun off)
    foreach (var branch in branches)
    {
        for (int dow = 0; dow <= 6; dow++)
        {
            var exists = await db.BranchOperatingHours.AnyAsync(
                h => h.BranchId == branch.Id && h.DayOfWeek == dow);
            if (exists) continue;

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
        await db.SaveChangesAsync();
    }

    // Customer tiers
    async Task EnsureTierAsync(string name, decimal minSpending, decimal discountPercent)
    {
        var loweredName = name.Trim().ToLowerInvariant();
        var existed = await db.CustomerTiers.AnyAsync(t => t.TierName != null && t.TierName.ToLower() == loweredName);
        if (existed) return;
        db.CustomerTiers.Add(new CustomerTier
        {
            TierName = name,
            MinSpending = minSpending,
            DiscountPercent = discountPercent,
            Benefits = $"Demo tier {name}",
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    await EnsureTierAsync("Bronze", 0m, 0m);
    await EnsureTierAsync("Silver", 5000000m, 3m);
    await EnsureTierAsync("Gold", 15000000m, 7m);

    // Service categories
    async Task<ServiceCategory> EnsureServiceCategoryAsync(string code, string name, string description)
    {
        var category = await db.ServiceCategories.FirstOrDefaultAsync(c => c.Code == code);
        if (category is not null) return category;

        category = new ServiceCategory
        {
            Code = code,
            Name = name,
            Description = description,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.ServiceCategories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    var catMaint = await EnsureServiceCategoryAsync("MAINT", "Báº£o dÆ°á»¡ng", "Dá»‹ch vá»¥ báº£o dÆ°á»¡ng Ä‘á»‹nh ká»³");
    var catRepair = await EnsureServiceCategoryAsync("REPAIR", "Sá»­a chá»¯a", "Dá»‹ch vá»¥ sá»­a chá»¯a");
    var catTire = await EnsureServiceCategoryAsync("TIRES", "Lá»‘p & Phanh", "Dá»‹ch vá»¥ lá»‘p vÃ  phanh");

    // Standard services
    async Task EnsureStandardServiceAsync(string code, string name, long categoryId, decimal basePrice, int standardMinutes)
    {
        var existed = await db.StandardServices.AnyAsync(s => s.Code == code);
        if (existed) return;
        db.StandardServices.Add(new StandardService
        {
            Code = code,
            Name = name,
            CategoryId = categoryId,
            BasePrice = basePrice,
            StandardMinutes = standardMinutes,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    await EnsureStandardServiceAsync("MAINT-OC", "Thay nhá»›t nhanh", catMaint.Id, 280000m, 40);
    await EnsureStandardServiceAsync("REP-ENG", "Cháº©n Ä‘oÃ¡n Ä‘á»™ng cÆ¡", catRepair.Id, 450000m, 60);
    await EnsureStandardServiceAsync("TIRE-BAL", "CÃ¢n báº±ng Ä‘á»™ng bÃ¡nh xe", catTire.Id, 220000m, 30);

    // Vehicle brands + models
    async Task<VehicleBrand> EnsureBrandAsync(string code, string name, string country)
    {
        var brand = await db.VehicleBrands.FirstOrDefaultAsync(b => b.Code == code);
        if (brand is not null) return brand;

        brand = new VehicleBrand
        {
            Code = code,
            Name = name,
            CountryOfOrigin = country,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.VehicleBrands.Add(brand);
        await db.SaveChangesAsync();
        return brand;
    }

    async Task<VehicleModel> EnsureModelAsync(long brandId, string modelName, string segment, string fuelType)
    {
        var model = await db.VehicleModels.FirstOrDefaultAsync(m => m.BrandId == brandId && m.ModelName == modelName);
        if (model is not null) return model;

        model = new VehicleModel
        {
            BrandId = brandId,
            ModelName = modelName,
            Segment = segment,
            FuelType = fuelType,
            CylinderCapacity = "1.5L",
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.VehicleModels.Add(model);
        await db.SaveChangesAsync();
        return model;
    }

    var toyota = await EnsureBrandAsync("TOY", "Toyota", "Japan");
    var honda = await EnsureBrandAsync("HON", "Honda", "Japan");
    var ford = await EnsureBrandAsync("FOR", "Ford", "USA");

    var vios = await EnsureModelAsync(toyota.Id, "Vios", "Sedan", "Gasoline");
    var city = await EnsureModelAsync(honda.Id, "City", "Sedan", "Gasoline");
    var ranger = await EnsureModelAsync(ford.Id, "Ranger", "Pickup", "Diesel");
    var demoModels = new[] { vios, city, ranger };

    // Staff demo data per branch
    var demoStaff = new[]
    {
        new { FullName = "Admin Báº¯c", Phone = "0901000001", Email = "admin.bac@mygarage.local", BranchId = northBranch.Id, RoleId = adminRole.Id },
        new { FullName = "Cá»‘ váº¥n Báº¯c", Phone = "0901000002", Email = "advisor.bac@mygarage.local", BranchId = northBranch.Id, RoleId = advisorRole.Id },
        new { FullName = "KTV Báº¯c", Phone = "0901000003", Email = "tech.bac@mygarage.local", BranchId = northBranch.Id, RoleId = techRole.Id },
        new { FullName = "Admin Trung", Phone = "0902000001", Email = "admin.trung@mygarage.local", BranchId = centralBranch.Id, RoleId = adminRole.Id },
        new { FullName = "Cá»‘ váº¥n Trung", Phone = "0902000002", Email = "advisor.trung@mygarage.local", BranchId = centralBranch.Id, RoleId = advisorRole.Id },
        new { FullName = "KTV Trung", Phone = "0902000003", Email = "tech.trung@mygarage.local", BranchId = centralBranch.Id, RoleId = techRole.Id },
        new { FullName = "Admin Nam", Phone = "0903000001", Email = "admin.nam@mygarage.local", BranchId = southBranch.Id, RoleId = adminRole.Id },
        new { FullName = "Cá»‘ váº¥n Nam", Phone = "0903000002", Email = "advisor.nam@mygarage.local", BranchId = southBranch.Id, RoleId = advisorRole.Id },
        new { FullName = "KTV Nam", Phone = "0903000003", Email = "tech.nam@mygarage.local", BranchId = southBranch.Id, RoleId = techRole.Id }
    };

    foreach (var item in demoStaff)
    {
        var loweredEmail = item.Email.Trim().ToLowerInvariant();
        var exists = await db.Staffs.AnyAsync(s => s.Email != null && s.Email.ToLower() == loweredEmail);
        if (exists) continue;

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
    }
    await db.SaveChangesAsync();

    var defaultTier = await db.CustomerTiers.OrderBy(t => t.Id).FirstOrDefaultAsync();
    var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

    // Customers + vehicles + ownership + appointments to bind branch-level data
    for (var i = 0; i < branches.Length; i++)
    {
        var branch = branches[i];
        for (var j = 1; j <= 2; j++)
        {
            var customerEmail = $"customer{j}.b{branch.Id}@mygarage.local";
            var customerPhone = $"091{branch.Id % 10}{j}000{j}";
            var customerName = $"KhÃ¡ch {j} - {branch.Name}";

            var loweredCustomerEmail = customerEmail.Trim().ToLowerInvariant();
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == loweredCustomerEmail);
            if (customer is null)
            {
                customer = new Customer
                {
                    FullName = customerName,
                    Phone = customerPhone,
                    Email = customerEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    TierId = defaultTier?.Id,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Customers.Add(customer);
                await db.SaveChangesAsync();
            }

            var model = demoModels[(i + j - 1) % demoModels.Length];
            var plate = $"{30 + i}{char.ConvertFromUtf32(65 + i)}-{12000 + i * 100 + j}";

            var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == plate);
            if (vehicle is null)
            {
                vehicle = new Vehicle
                {
                    LicensePlate = plate,
                    ModelId = model.Id,
                    Color = j % 2 == 0 ? "White" : "Black",
                    ManufactureYear = 2020 + j,
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Vehicles.Add(vehicle);
                await db.SaveChangesAsync();
            }

            var owned = await db.CustomerVehicles.AnyAsync(cv =>
                cv.CustomerId == customer.Id && cv.VehicleId == vehicle.Id && cv.IsDeleted != true);
            if (!owned)
            {
                db.CustomerVehicles.Add(new CustomerVehicle
                {
                    CustomerId = customer.Id,
                    VehicleId = vehicle.Id,
                    IsCurrentOwner = true,
                    Note = $"Demo ownership for {branch.Name}",
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                await db.SaveChangesAsync();
            }

            var hasAppointment = await db.Appointments.AnyAsync(a =>
                a.BranchId == branch.Id &&
                a.CustomerId == customer.Id &&
                a.VehicleId == vehicle.Id &&
                a.ScheduledDate == tomorrow);
            if (!hasAppointment)
            {
                db.Appointments.Add(new Appointment
                {
                    BranchId = branch.Id,
                    CustomerId = customer.Id,
                    VehicleId = vehicle.Id,
                    ScheduledDate = tomorrow,
                    StartTime = new TimeOnly(9 + j, 0),
                    EndTime = new TimeOnly(10 + j, 0),
                    Status = "PENDING",
                    CustomerNotes = $"Lá»‹ch háº¹n demo {branch.Name}",
                    IsReminderSent = false,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                await db.SaveChangesAsync();
            }
        }
    }
}


