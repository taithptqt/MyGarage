using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Common;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Data;

public partial class GarageContext : DbContext, IGarageContext
{
    private readonly IAuditContextAccessor? _auditContextAccessor;

    [ActivatorUtilitiesConstructor]
    public GarageContext(DbContextOptions<GarageContext> options, IAuditContextAccessor? auditContextAccessor = null)
        : base(options)
    {
        _auditContextAccessor = auditContextAccessor;
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HaveColumnType("decimal(18, 4)");
    }

    public virtual DbSet<RevokedAuthToken> RevokedAuthTokens { get; set; }

    public virtual DbSet<AiForecast> AiForecasts { get; set; }

    public virtual DbSet<AiFraudMonitor> AiFraudMonitors { get; set; }

    public virtual DbSet<AiKnowledgeBase> AiKnowledgeBases { get; set; }

    public virtual DbSet<AiModelConfig> AiModelConfigs { get; set; }

    public virtual DbSet<AiSmartDispatching> AiSmartDispatchings { get; set; }

    public virtual DbSet<Appointment> Appointments { get; set; }

    public virtual DbSet<AppointmentInsuranceAsset> AppointmentInsuranceAssets { get; set; }

    public virtual DbSet<AppointmentInsuranceUpload> AppointmentInsuranceUploads { get; set; }

    public virtual DbSet<GroupSession> GroupSessions { get; set; }

    public virtual DbSet<ApprovalRequest> ApprovalRequests { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Bay> Bays { get; set; }

    public virtual DbSet<CapacityConfig> CapacityConfigs { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<BranchQuotationApprovalPolicy> BranchQuotationApprovalPolicies { get; set; }

    public virtual DbSet<BranchException> BranchExceptions { get; set; }

    public virtual DbSet<BranchOperatingHour> BranchOperatingHours { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<ChatMessageAttachment> ChatMessageAttachments { get; set; }

    public virtual DbSet<ChatbotSession> ChatbotSessions { get; set; }

    public virtual DbSet<ChatbotMessage> ChatbotMessages { get; set; }

    public virtual DbSet<ChatbotActionLog> ChatbotActionLogs { get; set; }

    public virtual DbSet<ConversationCustomerRead> ConversationCustomerReads { get; set; }

    public virtual DbSet<ConversationStaffRead> ConversationStaffReads { get; set; }

    public virtual DbSet<CommissionRule> CommissionRules { get; set; }

    public virtual DbSet<CommissionTransaction> CommissionTransactions { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<CustomerFeedback> CustomerFeedbacks { get; set; }

    public virtual DbSet<CustomerTier> CustomerTiers { get; set; }

    public virtual DbSet<CustomerVehicle> CustomerVehicles { get; set; }

    public virtual DbSet<CustomerVoucher> CustomerVouchers { get; set; }

    public virtual DbSet<Delivery> Deliveries { get; set; }

    public virtual DbSet<DiagnosisRecommendation> DiagnosisRecommendations { get; set; }

    public virtual DbSet<DiagnosisAssignment> DiagnosisAssignments { get; set; }

    public virtual DbSet<Diagnosis> Diagnoses { get; set; }

    public virtual DbSet<ExpenseLog> ExpenseLogs { get; set; }

    public virtual DbSet<GatePass> GatePasses { get; set; }

    public virtual DbSet<InsuranceCompany> InsuranceCompanies { get; set; }

    public virtual DbSet<Inventory> Inventories { get; set; }

    public virtual DbSet<InventoryAudit> InventoryAudits { get; set; }

    public virtual DbSet<InventoryAuditDetail> InventoryAuditDetails { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceDetail> InvoiceDetails { get; set; }

    public virtual DbSet<MaterialRequest> MaterialRequests { get; set; }

    public virtual DbSet<MaterialRequestDetail> MaterialRequestDetails { get; set; }

    public virtual DbSet<MaterialReturn> MaterialReturns { get; set; }

    public virtual DbSet<ModelSpecificRule> ModelSpecificRules { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<NotificationTemplate> NotificationTemplates { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentMethod> PaymentMethods { get; set; }

    public virtual DbSet<Package> Packages { get; set; }

    public virtual DbSet<PackageItem> PackageItems { get; set; }

    public virtual DbSet<PartInventoryReservation> PartInventoryReservations { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public virtual DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }

    public virtual DbSet<QcInspection> QcInspections { get; set; }

    public virtual DbSet<Quotation> Quotations { get; set; }

    public virtual DbSet<QuotationDetail> QuotationDetails { get; set; }

    public virtual DbSet<QuotationDiscount> QuotationDiscounts { get; set; }

    public virtual DbSet<QuotationApprovalAction> QuotationApprovalActions { get; set; }

    public virtual DbSet<QuotationAccessLink> QuotationAccessLinks { get; set; }

    public virtual DbSet<MandatoryOverrideRequest> MandatoryOverrideRequests { get; set; }

    public virtual DbSet<RetailOrder> RetailOrders { get; set; }

    public virtual DbSet<RetailOrderItem> RetailOrderItems { get; set; }

    public virtual DbSet<RetailReturn> RetailReturns { get; set; }

    public virtual DbSet<RetailReturnItem> RetailReturnItems { get; set; }

    public virtual DbSet<RevenueSnapshot> RevenueSnapshots { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<ServiceCategory> ServiceCategories { get; set; }

    public virtual DbSet<ServiceOrder> ServiceOrders { get; set; }

    public virtual DbSet<ServiceOrderDetail> ServiceOrderDetails { get; set; }

    public virtual DbSet<ServiceOrderDiscount> ServiceOrderDiscounts { get; set; }

    public virtual DbSet<ServiceOrderInsuranceAsset> ServiceOrderInsuranceAssets { get; set; }

    public virtual DbSet<ServiceOrderInsuranceUpload> ServiceOrderInsuranceUploads { get; set; }

    public virtual DbSet<ServiceOrderSnapshot> ServiceOrderSnapshots { get; set; }

    public virtual DbSet<ServiceRefund> ServiceRefunds { get; set; }

    public virtual DbSet<ServiceStaffAssignment> ServiceStaffAssignments { get; set; }

    public virtual DbSet<Shift> Shifts { get; set; }

    public virtual DbSet<ShiftClosing> ShiftClosings { get; set; }

    public virtual DbSet<Staff> Staffs { get; set; }

    public virtual DbSet<Team> Teams { get; set; }

    public virtual DbSet<StaffSchedule> StaffSchedules { get; set; }

    public virtual DbSet<StaffTier> StaffTiers { get; set; }

    public virtual DbSet<StandardService> StandardServices { get; set; }

    public virtual DbSet<ServiceBom> ServiceBoms { get; set; }

    public virtual DbSet<StockTransaction> StockTransactions { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<Vehicle> Vehicles { get; set; }

    public virtual DbSet<VehicleBrand> VehicleBrands { get; set; }

    public virtual DbSet<VehicleConditionLog> VehicleConditionLogs { get; set; }

    public virtual DbSet<VehicleInsurance> VehicleInsurances { get; set; }

    public virtual DbSet<VehicleModel> VehicleModels { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    public virtual DbSet<VoucherApprovalRequest> VoucherApprovalRequests { get; set; }

    public virtual DbSet<VoucherAssignment> VoucherAssignments { get; set; }

    public virtual DbSet<VoucherAuditLog> VoucherAuditLogs { get; set; }

    public virtual DbSet<VoucherCampaign> VoucherCampaigns { get; set; }

    public virtual DbSet<VoucherCodeBatch> VoucherCodeBatches { get; set; }

    public virtual DbSet<VoucherConflictRule> VoucherConflictRules { get; set; }

    public virtual DbSet<VoucherRedemption> VoucherRedemptions { get; set; }

    public virtual DbSet<VoucherUsageRule> VoucherUsageRules { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    public virtual DbSet<WorkSlot> WorkSlots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureCoreAndAuth(modelBuilder);
        ConfigureCustomersAndVehicles(modelBuilder);
        ConfigureAppointmentsAndBays(modelBuilder);
        ConfigureQuotationsAndDiagnosis(modelBuilder);
        ConfigureWorkshop(modelBuilder);
        ConfigureInventoryAndPurchasing(modelBuilder);
        ConfigureRetailAndVouchers(modelBuilder);
        ConfigurePayrollAndAccounting(modelBuilder);
        ConfigureChatbotAndAi(modelBuilder);

        OnModelCreatingPartial(modelBuilder);
    }

    private void UpdateAuditFields()
    {
        var now = VietnamTime.Now;
        var userId = ResolveCurrentStaffId();

        ChangeTracker.DetectChanges();
        var entries = ChangeTracker.Entries()
            .Where(e =>
                !e.Metadata.IsOwned() &&
                (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var isAdded = entry.State == EntityState.Added;

            if (isAdded)
            {
                SetDateTimeProperty(entry, "CreatedAt", now, setOnlyWhenNull: true);
                SetLongProperty(entry, "CreatedBy", userId, setOnlyWhenNull: true);
                SetLongProperty(entry, "CreatedByStaffId", userId, setOnlyWhenNull: true);
                SetLongProperty(entry, "CreatedById", userId, setOnlyWhenNull: true);
            }

            SetDateTimeProperty(entry, "UpdatedAt", now, setOnlyWhenNull: false);
            SetLongProperty(entry, "UpdatedBy", userId, setOnlyWhenNull: false);
            SetLongProperty(entry, "UpdatedByStaffId", userId, setOnlyWhenNull: false);
            SetLongProperty(entry, "UpdatedById", userId, setOnlyWhenNull: false);
        }
    }

    private long? ResolveCurrentStaffId()
    {
        var parsedId = _auditContextAccessor?.GetCurrentContext()?.UserId;
        if (!parsedId.HasValue)
        {
            return null;
        }

        var userExistsInTrackedStaff = ChangeTracker.Entries<Staff>().Any(e => e.Entity.Id == parsedId.Value);
        var userExistsInDatabase = userExistsInTrackedStaff || Staffs.AsNoTracking().Any(s => s.Id == parsedId.Value);
        return userExistsInDatabase ? parsedId.Value : null;
    }

    private static void SetDateTimeProperty(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string propertyName, DateTime value, bool setOnlyWhenNull)
    {
        var property = entry.Properties.FirstOrDefault(p =>
            string.Equals(p.Metadata.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        if (property == null || property.Metadata.IsShadowProperty())
        {
            return;
        }

        var clrType = Nullable.GetUnderlyingType(property.Metadata.ClrType) ?? property.Metadata.ClrType;
        if (clrType != typeof(DateTime))
        {
            return;
        }

        if (setOnlyWhenNull && property.CurrentValue != null)
        {
            return;
        }

        property.CurrentValue = value;
    }

    private static void SetLongProperty(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string propertyName, long? value, bool setOnlyWhenNull)
    {
        var property = entry.Properties.FirstOrDefault(p =>
            string.Equals(p.Metadata.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        if (property == null || property.Metadata.IsShadowProperty())
        {
            return;
        }

        var clrType = Nullable.GetUnderlyingType(property.Metadata.ClrType) ?? property.Metadata.ClrType;
        if (clrType != typeof(long) && clrType != typeof(int))
        {
            return;
        }

        if (setOnlyWhenNull && property.CurrentValue != null)
        {
            return;
        }

        if (value == null)
        {
            property.CurrentValue = null;
            return;
        }

        property.CurrentValue = clrType == typeof(int) ? Convert.ToInt32(value.Value) : value.Value;
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
