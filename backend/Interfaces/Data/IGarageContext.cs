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
        DbSet<AiContentStore> AiContentStores { get; }
        DbSet<AiCustomerInsight> AiCustomerInsights { get; }
        DbSet<AiForecast> AiForecasts { get; }
        DbSet<AiFraudMonitor> AiFraudMonitors { get; }
        DbSet<AiKnowledgeBase> AiKnowledgeBases { get; }
        DbSet<AiLearningFeedback> AiLearningFeedbacks { get; }
        DbSet<AiModelConfig> AiModelConfigs { get; }
        DbSet<AiSmartDispatching> AiSmartDispatchings { get; }
        DbSet<AiTechnicalAssistant> AiTechnicalAssistants { get; }
        DbSet<Appointment> Appointments { get; }
        DbSet<GroupSession> GroupSessions { get; }
        DbSet<ApprovalRequest> ApprovalRequests { get; }
        DbSet<AuditLog> AuditLogs { get; }
        DbSet<Bay> Bays { get; }
        DbSet<Branch> Branches { get; }
        DbSet<BranchException> BranchExceptions { get; }
        DbSet<BranchOperatingHour> BranchOperatingHours { get; }
        DbSet<Category> Categories { get; }
        DbSet<ChatMessage> ChatMessages { get; }
        DbSet<CommissionRule> CommissionRules { get; }
        DbSet<CommissionTransaction> CommissionTransactions { get; }
        DbSet<Conversation> Conversations { get; }
        DbSet<Customer> Customers { get; }
        DbSet<CustomerFeedback> CustomerFeedbacks { get; }
        DbSet<CustomerTier> CustomerTiers { get; }
        DbSet<CustomerVehicle> CustomerVehicles { get; }
        DbSet<CustomerVoucher> CustomerVouchers { get; }
        DbSet<Delivery> Deliveries { get; }
        DbSet<DiagnoseRecommendation> DiagnoseRecommendations { get; }
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
        DbSet<Payment> Payments { get; }
        DbSet<PaymentMethod> PaymentMethods { get; }
        DbSet<Product> Products { get; }
        DbSet<ProductImage> ProductImages { get; }
        DbSet<PurchaseOrder> PurchaseOrders { get; }
        DbSet<PurchaseOrderItem> PurchaseOrderItems { get; }
        DbSet<QcInspection> QcInspections { get; }
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
        DbSet<ServiceOrderSnapshot> ServiceOrderSnapshots { get; }
        DbSet<ServiceRefund> ServiceRefunds { get; }
        DbSet<ServiceStaffAssignment> ServiceStaffAssignments { get; }
        DbSet<Shift> Shifts { get; }
        DbSet<ShiftClosing> ShiftClosings { get; }
        DbSet<Staff> Staffs { get; }
        DbSet<StaffSchedule> StaffSchedules { get; }
        DbSet<StaffTier> StaffTiers { get; }
        DbSet<StandardService> StandardServices { get; }
        DbSet<StockTransaction> StockTransactions { get; }
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
