using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IGarageContext
    {
        DbSet<AiForecast> AiForecasts { get; }
        DbSet<AiKnowledgeBase> AiKnowledgeBases { get; }
        DbSet<AiModelConfig> AiModelConfigs { get; }
        DbSet<AiSmartDispatching> AiSmartDispatchings { get; }
        DbSet<Appointment> Appointments { get; }
        DbSet<AppointmentInsuranceAsset> AppointmentInsuranceAssets { get; }
        DbSet<AppointmentInsuranceUpload> AppointmentInsuranceUploads { get; }
        DbSet<GroupSession> GroupSessions { get; }
        DbSet<ApprovalRequest> ApprovalRequests { get; }
        DbSet<AuditLog> AuditLogs { get; }
        DbSet<Bay> Bays { get; }
        DbSet<CapacityConfig> CapacityConfigs { get; }
        DbSet<Branch> Branches { get; }
        DbSet<BranchQuotationApprovalPolicy> BranchQuotationApprovalPolicies { get; }
        DbSet<BranchException> BranchExceptions { get; }
        DbSet<BranchOperatingHour> BranchOperatingHours { get; }
        DbSet<Category> Categories { get; }
        DbSet<ChatMessage> ChatMessages { get; }
        DbSet<ChatMessageAttachment> ChatMessageAttachments { get; }
        DbSet<ChatbotSession> ChatbotSessions { get; }
        DbSet<ChatbotMessage> ChatbotMessages { get; }
        DbSet<ChatbotActionLog> ChatbotActionLogs { get; }
        DbSet<ConversationCustomerRead> ConversationCustomerReads { get; }
        DbSet<ConversationStaffRead> ConversationStaffReads { get; }
        DbSet<CommissionRule> CommissionRules { get; }
        DbSet<CommissionTransaction> CommissionTransactions { get; }
        DbSet<Conversation> Conversations { get; }
        DbSet<Customer> Customers { get; }
        DbSet<CustomerFeedback> CustomerFeedbacks { get; }
        DbSet<CustomerTier> CustomerTiers { get; }
        DbSet<CustomerVehicle> CustomerVehicles { get; }
        DbSet<CustomerVoucher> CustomerVouchers { get; }
        DbSet<Delivery> Deliveries { get; }
        DbSet<DiagnosisRecommendation> DiagnosisRecommendations { get; }
        DbSet<DiagnosisAssignment> DiagnosisAssignments { get; }
        DbSet<Diagnosis> Diagnoses { get; }
        DbSet<ExpenseLog> ExpenseLogs { get; }
        DbSet<GatePass> GatePasses { get; }
        DbSet<InsuranceCompany> InsuranceCompanies { get; }
        DbSet<Inventory> Inventories { get; }
        DbSet<InventoryAudit> InventoryAudits { get; }
        DbSet<InventoryAuditDetail> InventoryAuditDetails { get; }
        DbSet<Invoice> Invoices { get; }
        DbSet<InvoiceDetail> InvoiceDetails { get; }
        DbSet<MaterialRequest> MaterialRequests { get; }
        DbSet<MaterialRequestDetail> MaterialRequestDetails { get; }
        DbSet<MaterialReturn> MaterialReturns { get; }
        DbSet<ModelSpecificRule> ModelSpecificRules { get; }
        DbSet<Notification> Notifications { get; }
        DbSet<NotificationTemplate> NotificationTemplates { get; }
        DbSet<MaintenanceReminder> MaintenanceReminders { get; }
        DbSet<Payment> Payments { get; }
        DbSet<PaymentMethod> PaymentMethods { get; }
        DbSet<Package> Packages { get; }
        DbSet<PackageItem> PackageItems { get; }
        DbSet<PartInventoryReservation> PartInventoryReservations { get; }
        DbSet<PasswordResetToken> PasswordResetTokens { get; }
        DbSet<Product> Products { get; }
        DbSet<ProductImage> ProductImages { get; }
        DbSet<PurchaseOrder> PurchaseOrders { get; }
        DbSet<PurchaseOrderItem> PurchaseOrderItems { get; }
        DbSet<GoodsReceiptNote> GoodsReceiptNotes { get; }
        DbSet<GoodsReceiptNoteItem> GoodsReceiptNoteItems { get; }
        DbSet<GrnDailySnapshot> GrnDailySnapshots { get; }
        DbSet<QcInspection> QcInspections { get; }
        DbSet<QcChecklistTemplate> QcChecklistTemplates { get; }
        DbSet<QcChecklistItem> QcChecklistItems { get; }
        DbSet<Quotation> Quotations { get; }
        DbSet<QuotationDetail> QuotationDetails { get; }
        DbSet<QuotationDiscount> QuotationDiscounts { get; }
        DbSet<QuotationApprovalAction> QuotationApprovalActions { get; }
        DbSet<QuotationAccessLink> QuotationAccessLinks { get; }
        DbSet<MandatoryOverrideRequest> MandatoryOverrideRequests { get; }
        DbSet<BayAllocation> BayAllocations { get; }
        DbSet<BayStatusLog> BayStatusLogs { get; }
        DbSet<ConsumableReturnBundle> ConsumableReturnBundles { get; }
        DbSet<ConsumableReturnBundleItem> ConsumableReturnBundleItems { get; }
        DbSet<TimesheetEntry> TimesheetEntries { get; }
        DbSet<RetailOrder> RetailOrders { get; }
        DbSet<RetailOrderItem> RetailOrderItems { get; }
        DbSet<RetailReturn> RetailReturns { get; }
        DbSet<RetailReturnItem> RetailReturnItems { get; }
        DbSet<RevenueSnapshot> RevenueSnapshots { get; }
        DbSet<Role> Roles { get; }
        DbSet<SalaryCoefficient> SalaryCoefficients { get; }
        DbSet<ServiceCategory> ServiceCategories { get; }
        DbSet<ServiceOrder> ServiceOrders { get; }
        DbSet<ServiceOrderDetail> ServiceOrderDetails { get; }
        DbSet<ServiceOrderDiscount> ServiceOrderDiscounts { get; }
        DbSet<ServiceOrderInsuranceAsset> ServiceOrderInsuranceAssets { get; }
        DbSet<ServiceOrderInsuranceUpload> ServiceOrderInsuranceUploads { get; }
        DbSet<ServiceOrderSnapshot> ServiceOrderSnapshots { get; }
        DbSet<ServiceRefund> ServiceRefunds { get; }
        DbSet<ServiceStaffAssignment> ServiceStaffAssignments { get; }
        DbSet<Shift> Shifts { get; }
        DbSet<ShiftClosing> ShiftClosings { get; }
        DbSet<Staff> Staffs { get; }
        DbSet<Team> Teams { get; }
        DbSet<StaffSchedule> StaffSchedules { get; }
        DbSet<StaffTier> StaffTiers { get; }
        DbSet<StandardService> StandardServices { get; }
        DbSet<ServiceBom> ServiceBoms { get; }
        DbSet<StockTransaction> StockTransactions { get; }
        DbSet<StockAlert> StockAlerts { get; }
        DbSet<Supplier> Suppliers { get; }
        DbSet<Vehicle> Vehicles { get; }
        DbSet<VehicleBrand> VehicleBrands { get; }
        DbSet<VehicleConditionLog> VehicleConditionLogs { get; }
        DbSet<VehicleInsurance> VehicleInsurances { get; }
        DbSet<VehicleModel> VehicleModels { get; }
        DbSet<Voucher> Vouchers { get; }
        DbSet<VoucherApprovalRequest> VoucherApprovalRequests { get; }
        DbSet<VoucherAssignment> VoucherAssignments { get; }
        DbSet<VoucherAuditLog> VoucherAuditLogs { get; }
        DbSet<VoucherCampaign> VoucherCampaigns { get; }
        DbSet<VoucherCodeBatch> VoucherCodeBatches { get; }
        DbSet<VoucherConflictRule> VoucherConflictRules { get; }
        DbSet<VoucherRedemption> VoucherRedemptions { get; }
        DbSet<VoucherUsageRule> VoucherUsageRules { get; }
        DbSet<Warehouse> Warehouses { get; }
        DbSet<WorkSlot> WorkSlots { get; }
        DbSet<PayrollPeriod> PayrollPeriods { get; }
        DbSet<PayrollRun> PayrollRuns { get; }
        DbSet<PayrollItem> PayrollItems { get; }
        DbSet<Payslip> Payslips { get; }
        DbSet<StaffSalaryProfile> StaffSalaryProfiles { get; }
        DbSet<StaffContract> StaffContracts { get; }
        DbSet<OvertimeEntry> OvertimeEntries { get; }
        DbSet<AttendanceAdjustment> AttendanceAdjustments { get; }
        DbSet<SalaryAdvance> SalaryAdvances { get; }
        DbSet<EmployeeLoan> EmployeeLoans { get; }
        DbSet<PitProfile> PitProfiles { get; }
        DbSet<EmployeeDependent> EmployeeDependents { get; }

        DatabaseFacade Database { get; }
        DbSet<TEntity> Set<TEntity>() where TEntity : class;
        EntityEntry Entry(object entity);

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
