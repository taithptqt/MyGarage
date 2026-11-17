using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Accounting;
using MyGarage.Application.Ai;
using MyGarage.Application.Bays;
using MyGarage.Application.Catalog;
using MyGarage.Application.CustomerPortal;
using MyGarage.Application.Customers;
using MyGarage.Application.GatePasses;
using MyGarage.Application.Interfaces.Common;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Inventory;
using MyGarage.Application.Invoices;
using MyGarage.Application.Notifications;
using MyGarage.Application.Payments;
using MyGarage.Application.Payroll;
using MyGarage.Application.Purchasing;
using MyGarage.Application.Retail;
using MyGarage.Application.Vehicles;
using MyGarage.Application.Vouchers;
using MyGarage.Data;
using MyGarage.Infrastructure.Extensions;
using MyGarage.Infrastructure.Queries;
using MyGarage.Interfaces;
using MyGarage.Options;
using MyGarage.Repositories;
using MyGarage.Services;

namespace MyGarage.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GarageDbConnection");

        services.AddGarageDbContext<GarageContext>(
            connectionString,
            sql => sql.MigrationsAssembly("MyGarage.Infrastructure"));
        services.AddScoped<IGarageContext>(provider => provider.GetRequiredService<GarageContext>());
        services.AddScoped<IDatabaseReadinessCheck, MyGarage.Infrastructure.Persistence.Common.EfDatabaseReadinessCheck>();

        services.AddOptions<CustomerAutomationJobsOptions>()
            .Bind(configuration.GetSection(CustomerAutomationJobsOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<LlmGatewayOptions>()
            .Bind(configuration.GetSection(LlmGatewayOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.GeminiAI))
                {
                    options.GeminiAI = new[]
                    {
                        configuration[$"{LlmGatewayOptions.SectionName}:geminiAI"],
                        configuration[$"{LlmGatewayOptions.SectionName}:GeminiAI"],
                        configuration["geminiAI"],
                        configuration["GeminiAI"]
                    }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
                }
            })
            .ValidateOnStart();

        services.AddHttpClient();

        // Infrastructure singletons
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<ISlotHoldService, SlotHoldService>();
        services.AddSingleton<IOrderStore, OrderService>();
        services.AddSingleton<IOrderTransactionStore, OrderTransactionService>();
        services.AddSingleton<IOrderInvoiceStore, OrderInvoiceService>();
        services.AddSingleton<IChatbotRateLimitHook, InMemoryChatbotRateLimitHook>();
        services.AddSingleton<IChatbotLlmProviderClient, OpenAiCompatibleChatbotLlmProviderClient>();
        services.AddSingleton(
            typeof(MyGarage.Application.Interfaces.Stores.Chatbot.IAppLogger<>),
            typeof(MyGarage.Infrastructure.Persistence.Stores.Chatbot.AppLoggerAdapter<>));
        services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();
        services.AddSingleton<IAuthRateLimitHook, InMemoryAuthRateLimitHook>();
        services.AddSingleton<IWriteRequestSpamGuard, InMemoryWriteRequestSpamGuard>();
        services.AddSingleton<IPersistenceExceptionClassifier, MyGarage.Infrastructure.Persistence.Common.EfPersistenceExceptionClassifier>();
        services.AddSingleton<MyGarage.Application.Interfaces.Stores.Common.IAppConfigFileStore, MyGarage.Infrastructure.Persistence.Stores.Common.FileAppConfigFileStore>();
        services.AddPayOsIntegration();

        // Query readers
        services.AddScoped<ISalaryProfileListReader, MyGarage.Infrastructure.Payroll.SalaryProfileListReader>();
        services.AddScoped<MyGarage.Application.Forecasts.IForecastListReader, MyGarage.Infrastructure.Ai.ForecastListReader>();
        services.AddScoped<IDispatchListReader, MyGarage.Infrastructure.Ai.DispatchListReader>();
        services.AddScoped<IDispatchBayCandidates, MyGarage.Infrastructure.Ai.DispatchBayCandidates>();
        services.AddScoped<IAiKnowledgePageReader, MyGarage.Infrastructure.Ai.AiKnowledgePageReader>();
        services.AddScoped<IAiKnowledgeCatalogReader, MyGarage.Infrastructure.Ai.AiKnowledgeCatalogReader>();
        services.AddScoped<IGatePassListReader, MyGarage.Infrastructure.GatePasses.GatePassListReader>();
        services.AddScoped<MyGarage.Application.Audit.IAdminAuditReader, MyGarage.Infrastructure.Audit.AdminAuditReader>();
        services.AddScoped<MyGarage.Application.Approvals.IApprovalListReader, MyGarage.Infrastructure.Approvals.ApprovalListReader>();
        services.AddScoped<IAiModelConfigPageReader, MyGarage.Infrastructure.Ai.AiModelConfigPageReader>();
        services.AddScoped<IInvoiceViewDetailReader, InvoiceViewDetailReader>();
        services.AddScoped<IServiceCategoryPageReader, ServiceCategoryPageReader>();
        services.AddScoped<IStandardServicePageReader, StandardServicePageReader>();
        services.AddScoped<IInvoiceQueryReader, InvoiceQueryReader>();
        services.AddScoped<IInvoiceTaxSummaryReader, InvoiceTaxSummaryReader>();
        services.AddScoped<IInvoiceListPageReader, InvoiceListPageReader>();
        services.AddScoped<ICustomerPaymentsReader, MyGarage.Infrastructure.Payments.CustomerPaymentsReader>();

        // EF query & data services
        services.AddScoped<IODataQueryService, ODataQueryService>();
        services.AddScoped<IAppointmentODataQuery>(sp => sp.GetRequiredService<IODataQueryService>());
        services.AddScoped<IAuditLogODataQuery>(sp => sp.GetRequiredService<IODataQueryService>());
        services.AddScoped<ICustomerODataQuery>(sp => sp.GetRequiredService<IODataQueryService>());
        services.AddScoped<IInventoryODataQuery>(sp => sp.GetRequiredService<IODataQueryService>());
        services.AddScoped<IQuotationODataQuery>(sp => sp.GetRequiredService<IODataQueryService>());
        services.AddScoped<IServiceOrderODataQuery>(sp => sp.GetRequiredService<IODataQueryService>());
        services.AddScoped<IStaffODataQuery>(sp => sp.GetRequiredService<IODataQueryService>());
        services.AddScoped<IVehicleODataQuery>(sp => sp.GetRequiredService<IODataQueryService>());
        services.AddScoped<ICustomerVoucherQueryService, EfCustomerVoucherQueryService>();
        services.AddScoped<ICustomerVehicleQueryService, EfCustomerVehicleQueryService>();
        services.AddScoped<ICustomerFeedbackQueryService, EfCustomerFeedbackQueryService>();
        services.AddScoped<IPurchaseOrderItemQueryService, EfPurchaseOrderItemQueryService>();
        services.AddScoped<IPayslipQueryService, EfPayslipQueryService>();
        services.AddScoped<ISupplierQueryService, EfSupplierQueryService>();
        services.AddScoped<ICategoryQueryService, EfCategoryQueryService>();
        services.AddScoped<IBranchAdminQueryService, EfBranchAdminQueryService>();
        services.AddScoped<IVehicleModelAdminQueryService, EfVehicleModelAdminQueryService>();
        services.AddScoped<IVehicleBrandAdminQueryService, EfVehicleBrandAdminQueryService>();
        services.AddScoped<IPaymentQueryService, EfPaymentQueryService>();
        services.AddScoped<IStaffQueryService, EfStaffQueryService>();
        services.AddScoped<IVoucherApprovalRequestQueryService, EfVoucherApprovalRequestQueryService>();
        services.AddScoped<IVehicleQueryService, EfVehicleQueryService>();
        services.AddScoped<ITimesheetQueryService, EfTimesheetQueryService>();
        services.AddScoped<IReceptionAppointmentListQueryService, EfReceptionAppointmentListQueryService>();
        services.AddScoped<IMaterialRequestDetailQueryService, EfMaterialRequestDetailQueryService>();
        services.AddScoped<ICustomerQueryService, EfCustomerQueryService>();
        services.AddScoped<IStaffSpecialtyDataService, EfStaffSpecialtyDataService>();
        services.AddScoped<IShiftClosingDataService, EfShiftClosingDataService>();
        services.AddScoped<IVehicleConditionLogQueryService, EfVehicleConditionLogQueryService>();
        services.AddScoped<ISalaryAdminDataService, EfSalaryAdminDataService>();
        services.AddScoped<IWebhookPaymentDataService, EfWebhookPaymentDataService>();
        services.AddScoped<ICommissionTransactionQueryService, EfCommissionTransactionQueryService>();
        services.AddScoped<IVoucherCampaignQueryService, EfVoucherCampaignQueryService>();
        services.AddScoped<IMaterialRequestQueryService, EfMaterialRequestQueryService>();
        services.AddScoped<ICommissionRuleDataService, EfCommissionRuleDataService>();
        services.AddScoped<IVoucherRedemptionQueryService, EfVoucherRedemptionQueryService>();
        services.AddScoped<IRetailReturnItemQueryService, EfRetailReturnItemQueryService>();
        services.AddScoped<IAdminPartsDataService, EfAdminPartsDataService>();
        services.AddScoped<IProductQueryService, EfProductQueryService>();
        services.AddScoped<IQcChecklistTemplateDataService, EfQcChecklistTemplateDataService>();
        services.AddScoped<IQcInspectionDataService, EfQcInspectionDataService>();
        services.AddScoped<IRetailOrderDataService, EfRetailOrderDataService>();
        services.AddScoped<IAppointmentDataService, EfAppointmentDataService>();
        services.AddScoped<IQuotationDataService, EfQuotationDataService>();
        services.AddScoped<IAccountingDataService, EfAccountingDataService>();
        services.AddScoped<IRetailReturnDataService, EfRetailReturnDataService>();
        services.AddScoped<IPayrollRunDataService, EfPayrollRunDataService>();
        services.AddScoped<IDiagnosisRecommendationDataService, EfDiagnosisRecommendationDataService>();
        services.AddScoped<IOrderDataService, EfOrderDataService>();
        services.AddScoped<IServiceStaffAssignmentDataService, EfServiceStaffAssignmentDataService>();
        services.AddScoped<IServiceStaffAssignmentQueryDataService>(sp => sp.GetRequiredService<IServiceStaffAssignmentDataService>());
        services.AddScoped<IServiceStaffAssignmentRosterDataService>(sp => sp.GetRequiredService<IServiceStaffAssignmentDataService>());
        services.AddScoped<IServiceStaffAssignmentLifecycleDataService>(sp => sp.GetRequiredService<IServiceStaffAssignmentDataService>());
        services.AddScoped<IServiceOrderDataService, EfServiceOrderDataService>();
        services.AddScoped<IBayAdminDataService, EfBayAdminDataService>();
        services.AddScoped<MyGarage.Application.Bays.IVehicleLocationService, MyGarage.Services.VehicleLocationService>();
        services.AddScoped<IBayVehicleLookupDataService>(sp => sp.GetRequiredService<IBayAdminDataService>());
        services.AddScoped<IBayAdminReadDataService>(sp => sp.GetRequiredService<IBayAdminDataService>());
        services.AddScoped<IBayAdminWriteDataService>(sp => sp.GetRequiredService<IBayAdminDataService>());
        services.AddScoped<IVehicleAdminDataService, EfVehicleAdminDataService>();
        services.AddScoped<IVehicleAdminLookupQueryService>(sp => sp.GetRequiredService<IVehicleAdminDataService>());
        services.AddScoped<IVehicleAdminBayStaffQueryService>(sp => sp.GetRequiredService<IVehicleAdminDataService>());
        services.AddScoped<IVehicleAdminEntityDataService>(sp => sp.GetRequiredService<IVehicleAdminDataService>());

        // Repositories
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IServiceOrderRepository, ServiceOrderRepository>();
        services.AddScoped<IServiceOrderDetailRepository, ServiceOrderDetailRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IServiceCategoryRepository, ServiceCategoryRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IStaffScheduleRepository, StaffScheduleRepository>();
        services.AddScoped<IAiForecastRepository, AiForecastRepository>();
        services.AddScoped<IAiKnowledgeBaseRepository, AiKnowledgeBaseRepository>();
        services.AddScoped<IAiModelConfigRepository, AiModelConfigRepository>();
        services.AddScoped<IAiSmartDispatchingRepository, AiSmartDispatchingRepository>();
        services.AddScoped<IApprovalRequestRepository, ApprovalRequestRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IBayRepository, BayRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IBranchExceptionRepository, BranchExceptionRepository>();
        services.AddScoped<IBranchOperatingHourRepository, BranchOperatingHourRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<ICommissionRuleRepository, CommissionRuleRepository>();
        services.AddScoped<ICommissionTransactionRepository, CommissionTransactionRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<ICustomerFeedbackRepository, CustomerFeedbackRepository>();
        services.AddScoped<ICustomerTierRepository, CustomerTierRepository>();
        services.AddScoped<ICustomerVehicleRepository, CustomerVehicleRepository>();
        services.AddScoped<ICustomerVoucherRepository, CustomerVoucherRepository>();
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<IDiagnosisRecommendationRepository, DiagnosisRecommendationRepository>();
        services.AddScoped<IDiagnosisRepository, DiagnosisRepository>();
        services.AddScoped<IExpenseLogRepository, ExpenseLogRepository>();
        services.AddScoped<IGatePassRepository, GatePassRepository>();
        services.AddScoped<IInsuranceCompanyRepository, InsuranceCompanyRepository>();
        services.AddScoped<IInventoryAuditRepository, InventoryAuditRepository>();
        services.AddScoped<IInventoryAuditDetailRepository, InventoryAuditDetailRepository>();
        services.AddScoped<IInvoiceDetailRepository, InvoiceDetailRepository>();
        services.AddScoped<IMaterialRequestRepository, MaterialRequestRepository>();
        services.AddScoped<IMaterialRequestDetailRepository, MaterialRequestDetailRepository>();
        services.AddScoped<IMaterialReturnRepository, MaterialReturnRepository>();
        services.AddScoped<IModelSpecificRuleRepository, ModelSpecificRuleRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
        services.AddScoped<IProductImageRepository, ProductImageRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IPurchaseOrderItemRepository, PurchaseOrderItemRepository>();
        services.AddScoped<IQcInspectionRepository, QcInspectionRepository>();
        services.AddScoped<IRetailOrderRepository, RetailOrderRepository>();
        services.AddScoped<IRetailOrderItemRepository, RetailOrderItemRepository>();
        services.AddScoped<IRetailReturnRepository, RetailReturnRepository>();
        services.AddScoped<IRetailReturnItemRepository, RetailReturnItemRepository>();
        services.AddScoped<IRevenueSnapshotRepository, RevenueSnapshotRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IServiceOrderDiscountRepository, ServiceOrderDiscountRepository>();
        services.AddScoped<IServiceOrderSnapshotRepository, ServiceOrderSnapshotRepository>();
        services.AddScoped<IServiceRefundRepository, ServiceRefundRepository>();
        services.AddScoped<IServiceStaffAssignmentRepository, ServiceStaffAssignmentRepository>();
        services.AddScoped<IShiftRepository, ShiftRepository>();
        services.AddScoped<IShiftClosingRepository, ShiftClosingRepository>();
        services.AddScoped<IStaffTierRepository, StaffTierRepository>();
        services.AddScoped<ISalaryCoefficientRepository, SalaryCoefficientRepository>();
        services.AddScoped<IStandardServiceRepository, StandardServiceRepository>();
        services.AddScoped<IPackageRepository, PackageRepository>();
        services.AddScoped<IStockTransactionRepository, StockTransactionRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IVehicleBrandRepository, VehicleBrandRepository>();
        services.AddScoped<IVehicleConditionLogRepository, VehicleConditionLogRepository>();
        services.AddScoped<IVehicleInsuranceRepository, VehicleInsuranceRepository>();
        services.AddScoped<IVehicleModelRepository, VehicleModelRepository>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IVoucherCampaignRepository, VoucherCampaignRepository>();
        services.AddScoped<IVoucherApprovalRequestRepository, VoucherApprovalRequestRepository>();
        services.AddScoped<IVoucherRedemptionRepository, VoucherRedemptionRepository>();
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IWorkSlotRepository, WorkSlotRepository>();

        // EF Stores
        services.AddScoped<IBayStore, MyGarage.Infrastructure.Bays.EfBayStore>();
        services.AddScoped<IBayQueryStore>(sp => sp.GetRequiredService<IBayStore>());
        services.AddScoped<IBayCommandStore>(sp => sp.GetRequiredService<IBayStore>());
        services.AddScoped<IBayAllocationStore>(sp => sp.GetRequiredService<IBayStore>());
        services.AddScoped<IInventoryStore, MyGarage.Infrastructure.Inventory.EfInventoryStore>();
        services.AddScoped<IInventoryCatalogStore>(sp => sp.GetRequiredService<IInventoryStore>());
        services.AddScoped<IInventoryStockAlertStore>(sp => sp.GetRequiredService<IInventoryStore>());
        services.AddScoped<IInventoryOperationsStore>(sp => sp.GetRequiredService<IInventoryStore>());
        services.AddScoped<IInventoryAdminStore>(sp => sp.GetRequiredService<IInventoryStore>());
        services.AddScoped<IPurchaseOrderStore, MyGarage.Infrastructure.Purchasing.EfPurchaseOrderStore>();
        services.AddScoped<IPurchaseOrderTransactionStore>(sp => sp.GetRequiredService<IPurchaseOrderStore>());
        services.AddScoped<IPurchaseOrderQueryStore>(sp => sp.GetRequiredService<IPurchaseOrderStore>());
        services.AddScoped<IPurchaseOrderCommandStore>(sp => sp.GetRequiredService<IPurchaseOrderStore>());
        services.AddScoped<IPurchaseOrderGoodsReceiptStore>(sp => sp.GetRequiredService<IPurchaseOrderStore>());
        services.AddScoped<IGoodsReceiptStore, MyGarage.Infrastructure.Purchasing.EfGoodsReceiptStore>();
        services.AddScoped<IRetailOrderStore, MyGarage.Infrastructure.Retail.EfRetailOrderStore>();
        services.AddScoped<IVoucherStore, MyGarage.Infrastructure.Vouchers.EfVoucherStore>();
        services.AddScoped<IVoucherCatalogStore>(sp => sp.GetRequiredService<IVoucherStore>());
        services.AddScoped<IVoucherCampaignStore>(sp => sp.GetRequiredService<IVoucherStore>());
        services.AddScoped<IVoucherRedemptionStore>(sp => sp.GetRequiredService<IVoucherStore>());
        services.AddScoped<IVoucherValidationStore>(sp => sp.GetRequiredService<IVoucherStore>());
        services.AddScoped<IPaymentStore, MyGarage.Infrastructure.Payments.EfPaymentStore>();
        services.AddScoped<IPaymentCoreStore>(sp => sp.GetRequiredService<IPaymentStore>());
        services.AddScoped<IPaymentAuthorizationStore>(sp => sp.GetRequiredService<IPaymentStore>());
        services.AddScoped<IPaymentSettlementStore>(sp => sp.GetRequiredService<IPaymentStore>());
        services.AddScoped<IPaymentNotificationQueryStore>(sp => sp.GetRequiredService<IPaymentStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Services.Chat.IChatHubAccessService, MyGarage.Infrastructure.Persistence.Stores.Chat.EfChatHubAccessService>();
        services.AddScoped<IInvoiceStore, MyGarage.Infrastructure.Invoices.EfInvoiceStore>();
        services.AddScoped<IInvoiceBranchLookupStore>(sp => sp.GetRequiredService<IInvoiceStore>());
        services.AddScoped<IInvoiceApplicationStore>(sp => sp.GetRequiredService<IInvoiceStore>());
        services.AddScoped<IInvoiceAuthorizationStore>(sp => sp.GetRequiredService<IInvoiceStore>());
        services.AddScoped<IInvoiceBackfillStore>(sp => sp.GetRequiredService<IInvoiceStore>());
        services.AddScoped<IInvoiceOrderContextStore>(sp => sp.GetRequiredService<IInvoiceStore>());
        services.AddScoped<IInvoicePersistenceStore>(sp => sp.GetRequiredService<IInvoiceStore>());
        services.AddScoped<IAccountingStore, MyGarage.Infrastructure.Accounting.EfAccountingStore>();
        services.AddScoped<ICustomerStore, MyGarage.Infrastructure.Customers.EfCustomerStore>();
        services.AddScoped<IVehicleStore, MyGarage.Infrastructure.Vehicles.EfVehicleStore>();
        services.AddScoped<ICatalogStore, MyGarage.Infrastructure.Catalog.EfCatalogStore>();
        services.AddScoped<IPayrollStore, MyGarage.Infrastructure.Payroll.EfPayrollStore>();
        services.AddScoped<IPayrollTransactionStore>(sp => sp.GetRequiredService<IPayrollStore>());
        services.AddScoped<ISalaryProfileStore>(sp => sp.GetRequiredService<IPayrollStore>());
        services.AddScoped<IPayrollRunStore>(sp => sp.GetRequiredService<IPayrollStore>());
        services.AddScoped<IPayrollCalculationDataStore>(sp => sp.GetRequiredService<IPayrollStore>());
        services.AddScoped<ICustomerPortalStore, MyGarage.Infrastructure.CustomerPortal.EfCustomerPortalStore>();
        services.AddScoped<ICustomerPortalAuthorizationStore>(sp => sp.GetRequiredService<ICustomerPortalStore>());
        services.AddScoped<ICustomerPortalVehicleStore>(sp => sp.GetRequiredService<ICustomerPortalStore>());
        services.AddScoped<ICustomerPortalNotificationStore>(sp => sp.GetRequiredService<ICustomerPortalStore>());
        services.AddScoped<ICustomerPortalDashboardStore>(sp => sp.GetRequiredService<ICustomerPortalStore>());
        services.AddScoped<INotificationStore, MyGarage.Infrastructure.Notifications.EfNotificationStore>();
        services.AddScoped<IAiStore, MyGarage.Infrastructure.Ai.EfAiStore>();
        services.AddScoped<IAiApplicationStore>(sp => sp.GetRequiredService<IAiStore>());
        services.AddScoped<IAiChatbotSessionStore>(sp => sp.GetRequiredService<IAiStore>());
        services.AddScoped<IGatePassAndApprovalStore, MyGarage.Infrastructure.GatePasses.EfGatePassAndApprovalStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Retail.IRetailOrderAuthorizationStore, MyGarage.Infrastructure.Persistence.Stores.Retail.EfRetailOrderAuthorizationStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Purchasing.IPurchaseOrderAuthorizationStore, MyGarage.Infrastructure.Persistence.Stores.Purchasing.EfPurchaseOrderAuthorizationStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Purchasing.IPurchaseOrderReceiveStore, MyGarage.Infrastructure.Persistence.Stores.Purchasing.EfPurchaseOrderReceiveStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Chat.IChatStore, MyGarage.Infrastructure.Persistence.Stores.Chat.EfChatStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Chat.IChatConversationStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Chat.IChatStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Chat.IChatMessageStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Chat.IChatStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Chat.IChatReadStateStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Chat.IChatStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Chat.IChatContextStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Chat.IChatStore>());
        services.AddScoped<MyGarage.Interfaces.IChatCallerCustomerResolver, MyGarage.Infrastructure.Services.Application.Chat.ChatCallerCustomerResolver>();
        services.AddScoped<IBayAuthorizationStore, MyGarage.Infrastructure.Persistence.Stores.StaffAndBranches.EfBayAuthorizationStore>();
        services.AddScoped<IStaffStore, MyGarage.Infrastructure.Persistence.Stores.StaffAndBranches.EfStaffStore>();
        services.AddScoped<IAuditLogStore, MyGarage.Infrastructure.Persistence.Stores.Common.EfAuditLogStore>();
        services.AddScoped<ISharedOperationsAuthorizationStore, MyGarage.Infrastructure.Persistence.Stores.Common.EfSharedOperationsAuthorizationStore>();
        services.AddScoped<IVehicleAuthorizationStore, MyGarage.Infrastructure.Persistence.Stores.Vehicles.EfVehicleAuthorizationStore>();
        services.AddScoped<IVehicleConditionLogStore, MyGarage.Infrastructure.Persistence.Stores.Vehicles.EfVehicleConditionLogStore>();
        services.AddScoped<IVehicleHistoryStore, MyGarage.Infrastructure.Persistence.Stores.Vehicles.EfVehicleHistoryStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Auth.IAuthStore, MyGarage.Infrastructure.Persistence.Stores.Auth.EfAuthStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Auth.IAuthSessionValidationStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Auth.IAuthStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Auth.IAuthCustomerAccountStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Auth.IAuthStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Auth.IAuthStaffAccountStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Auth.IAuthStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Auth.IAuthPasswordResetStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Auth.IAuthStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Chatbot.IChatbotStore, MyGarage.Infrastructure.Persistence.Stores.Chatbot.EfChatbotStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Chatbot.IChatbotSessionStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Chatbot.IChatbotStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Chatbot.IChatbotLoyaltyVoucherStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Chatbot.IChatbotStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Chatbot.IChatbotToolQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Chatbot.IChatbotStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Chatbot.IChatbotBookingStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Chatbot.IChatbotStore>());
        services.AddScoped<IMaintenanceReminderStore, MyGarage.Infrastructure.Persistence.Stores.Appointments.EfMaintenanceReminderStore>();
        services.AddScoped<IAppointmentAuthorizationStore, MyGarage.Infrastructure.Persistence.Stores.Appointments.EfAppointmentAuthorizationStore>();
        services.AddScoped<IGroupSessionStore, MyGarage.Infrastructure.Persistence.Stores.Appointments.EfGroupSessionStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentStore, MyGarage.Infrastructure.Persistence.Stores.Appointments.EfAppointmentStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentCommandStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentScheduleStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentBranchCapacityStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentScheduleQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentWorkSlotStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentCatalogStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentContextStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentCustomerVehicleStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentInsuranceStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentOperationalContextStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Appointments.IAppointmentStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IWorkshopAuthorizationStore, MyGarage.Infrastructure.Persistence.Stores.Workshop.EfWorkshopAuthorizationStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderDetailStore, MyGarage.Infrastructure.Persistence.Stores.Workshop.EfServiceOrderDetailStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IQcInspectionStore, MyGarage.Infrastructure.Persistence.Stores.Workshop.EfQcInspectionStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderStore, MyGarage.Infrastructure.Persistence.Stores.Workshop.EfServiceOrderStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderCommandStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderTransactionCommandStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderCommandStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderCreationCommandStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderCommandStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderBillingCommandStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderCommandStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderCommissionCommandStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderCommandStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderIntakeCommandStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderCommandStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderStatusQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderQueryStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderWorkflowQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderQueryStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderHandoverQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderQueryStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderBillingQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderQueryStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderIntakeQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderQueryStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderEditCapabilityQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderQueryStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderIntakeContextStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderCommissionStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IServiceOrderStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationWorkflowStore, MyGarage.Infrastructure.Persistence.Stores.Workshop.EfQuotationWorkflowStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationBuildStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationWorkflowStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationBuildCommandStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationBuildStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationBuildSourceStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationBuildStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationApprovalStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationWorkflowStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationApprovalDiscountStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationApprovalStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationApprovalPolicyStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationApprovalStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationApprovalOrderSyncStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationApprovalStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationPortalStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationWorkflowStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationLineageQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IQuotationWorkflowStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisStore, MyGarage.Infrastructure.Persistence.Stores.Workshop.EfDiagnosisStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisCommandStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisAssignmentQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisQueryStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisRecordQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisQueryStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisSnapshotSignalQueryStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisQueryStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisOrderContextStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisDispatchResourceStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisDispatchTeamStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisDispatchResourceStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisDispatchStaffStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisDispatchResourceStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisDispatchBaySlotStore>(sp => sp.GetRequiredService<MyGarage.Application.Interfaces.Stores.Workshop.IDiagnosisDispatchResourceStore>());
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IBayManagementStore, MyGarage.Infrastructure.Persistence.Stores.Workshop.EfBayManagementStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IConsumableReturnStore, MyGarage.Infrastructure.Persistence.Stores.Workshop.EfConsumableReturnStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.ITechnicianProductivityStore, MyGarage.Infrastructure.Persistence.Stores.Workshop.EfTechnicianProductivityStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.ITimesheetStore, MyGarage.Infrastructure.Persistence.Stores.Workshop.EfTimesheetStore>();
        services.AddScoped<MyGarage.Application.Interfaces.Stores.Workshop.IWorkshopDispatchStore, MyGarage.Infrastructure.Persistence.Stores.Workshop.EfWorkshopDispatchStore>();

        // Infrastructure adapters & services
        services.AddScoped<IAccountingCrudService, AccountingCrudService>();
        services.AddScoped<IServiceOrderCustomerLifecycleNotifier, ServiceOrderCustomerLifecycleNotifier>();
        services.AddScoped<ITechnicianProductivityService, TechnicianProductivityService>();
        services.AddScoped<IAppointmentStatusPolicy, AppointmentStatusPolicy>();
        services.AddScoped<IAppointmentDetailAssembler, AppointmentDetailAssembler>();
        services.AddScoped<IGroupSessionStatusPolicy, GroupSessionStatusPolicy>();
        services.AddScoped<ICustomerAutomationJobProcessor, CustomerAutomationJobProcessor>();
        services.AddScoped<ITimesheetService, TimesheetService>();
        services.AddScoped<IPasswordHasher, MyGarage.Infrastructure.Services.Security.BcryptPasswordHasher>();
        services.AddScoped<IChatbotImageUploadService, ChatbotImageUploadService>();
        services.AddScoped<IVisionAnalyzer, HeuristicVisionAnalyzer>();
        services.AddScoped<IFileSecurityScanner, MagicBytesFileSecurityScanner>();
        services.AddScoped<IQuotationExcelService, MyGarage.Infrastructure.Documents.ClosedXmlQuotationExcelService>();
        services.AddScoped<IChatbotRetrievalService, ChatbotRetrievalService>();
        services.AddScoped<IChatbotLlmPersistence, EfChatbotLlmPersistence>();
        services.AddScoped<IChatbotLlmGateway, ChatbotLlmGateway>();
        services.AddScoped<ICloudStorageService, AzureBlobStorageService>();
        services.AddScoped<IJwtTokenService, MyGarage.Infrastructure.Persistence.Stores.Auth.JwtTokenService>();
        services.AddScoped<IGoogleTokenVerifier, MyGarage.Infrastructure.Persistence.Stores.Auth.GoogleTokenVerifier>();
        services.AddScoped<IAuthConfigProvider, MyGarage.Infrastructure.Persistence.Stores.Auth.AuthConfigProvider>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPayOsGateway, MyGarage.Infrastructure.Services.Payments.PayOsGateway>();

        // Background services
        if (!configuration.GetValue<bool>("Diagnostics:DisableBackgroundJobs"))
        {
            services.AddHostedService<AppointmentDispatchBackgroundService>();
            services.AddHostedService<AppointmentReminderBackgroundService>();
            services.AddHostedService<AppointmentInsuranceUploadCleanupBackgroundService>();
            services.AddHostedService<ServiceOrderInsuranceUploadCleanupBackgroundService>();
            services.AddHostedService<CustomerAutomationJobsBackgroundService>();
            services.AddHostedService<RetailOrderTimeoutBackgroundService>();
        }

        return services;
    }

    private static IServiceCollection AddGarageDbContext<TContext>(
        this IServiceCollection services,
        string? connectionString,
        Action<SqlServerDbContextOptionsBuilder>? extra = null)
        where TContext : DbContext
    {
        return services.AddDbContext<TContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(15),
                    errorNumbersToAdd: null);
                extra?.Invoke(sql);
            }));
    }
}
