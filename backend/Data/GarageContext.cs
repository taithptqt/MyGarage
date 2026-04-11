using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using MyGarage.Interfaces;

namespace MyGarage.Data;

public partial class GarageContext : DbContext, IGarageContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GarageContext(DbContextOptions<GarageContext> options, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

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

    public virtual DbSet<DiagnoseRecommendation> DiagnoseRecommendations { get; set; }

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
        modelBuilder.Entity<AiForecast>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AI_Forec__3214EC07A43540EC");

            entity.ToTable("AI_Forecasts");

            entity.Property(e => e.ConfidenceScore).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Vehicle).WithMany(p => p.AiForecasts)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AI_Foreca__Vehic__1758727B");
        });

        modelBuilder.Entity<AiFraudMonitor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AI_Fraud__3214EC0761F06F3E");

            entity.ToTable("AI_FraudMonitors");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.EntityType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RiskLevel)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(AiFraudMonitorStatusCode.FLAGGED));
        });

        modelBuilder.Entity<AiKnowledgeBase>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AI_Knowl__3214EC0754A71E47");

            entity.ToTable("AI_KnowledgeBase");

            entity.Property(e => e.IsDynamic).HasDefaultValue(false);
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.SourceUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Topic).HasMaxLength(255);
        });

        modelBuilder.Entity<AiModelConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AI_Model__3214EC07716BF4FE");

            entity.ToTable("AI_ModelConfigs");

            entity.Property(e => e.ApiKey)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ModelName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Provider)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Temperature).HasColumnType("decimal(3, 2)");
        });

        modelBuilder.Entity<AiSmartDispatching>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AI_Smart__3214EC07DC395169");

            entity.ToTable("AI_SmartDispatching");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.EfficiencyScore).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(AiDispatchStatus.GENERATED));

            entity.HasOne(d => d.Branch).WithMany(p => p.AiSmartDispatchings)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AI_SmartD__Branc__184C96B4");
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Appointm__3214EC0762F6D2AE");

            entity.HasIndex(e => e.GroupSessionId, "IX_Appointments_GroupSessionId");

            entity.Property(e => e.GroupSessionId).HasColumnName("GroupSessionId");
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.InsuranceClaimMode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.InsuranceDeductibleAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceDeductibleOption)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.InsuranceQuoteState)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.IsReminderSent).HasDefaultValue(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(AppointmentStatus.PENDING));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Appointme__Branc__1B29035F");

            entity.HasOne(d => d.GroupSession).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.GroupSessionId)
                .HasConstraintName("FK_Appointments_GroupSessionId");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Appointme__Creat__1E05700A");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AppointmentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy);

            entity.HasOne(d => d.Customer).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Appointme__Custo__1C1D2798");

            entity.HasOne(d => d.InsuranceCompany).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.InsuranceCompanyId)
                .HasConstraintName("FK_Appointments_InsuranceCompanyId");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK__Appointme__Vehic__1D114BD1");
        });

        modelBuilder.Entity<AppointmentInsuranceAsset>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_AppointmentInsuranceAssets");

            entity.HasIndex(e => new { e.AppointmentId, e.AssetType, e.SortOrder }, "IX_AppointmentInsuranceAssets_Appointment_Type_Order");

            entity.Property(e => e.AssetType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Appointment).WithMany(p => p.InsuranceAssets)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AppointmentInsuranceAssets_Appointments");
        });

        modelBuilder.Entity<AppointmentInsuranceUpload>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_AppointmentInsuranceUploads");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_AppointmentInsuranceUploads_Status_CreatedAt");

            entity.Property(e => e.AssetType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.BlobPath)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Appointment).WithMany(p => p.InsuranceUploads)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_AppointmentInsuranceUploads_Appointments");
        });

        modelBuilder.Entity<GroupSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BookingG__3214EC077B6FE4D1");
            entity.ToTable("GroupSessions", tb =>
            {
                tb.HasCheckConstraint(
                    "CK_BookingGroups_Status",
                    $"[Status] IN ('{nameof(BookingGroupStatus.PENDING)}','{nameof(BookingGroupStatus.CONFIRMED)}','{nameof(BookingGroupStatus.PARTIAL_CONFIRMED)}','{nameof(BookingGroupStatus.CANCELED)}','{nameof(BookingGroupStatus.COMPLETED)}')");
                tb.HasCheckConstraint("CK_GroupSessions_ItemCounters", "[TotalItems] >= 0 AND [ConfirmedItems] >= 0 AND [CanceledItems] >= 0 AND [ConfirmedItems] + [CanceledItems] <= [TotalItems]");
            });

            entity.HasIndex(e => e.GroupCode, "UQ_GroupSessions_GroupCode").IsUnique();

            entity.HasIndex(e => new { e.CustomerId, e.CreatedAt }, "IX_GroupSessions_CustomerId_CreatedAt");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_GroupSessions_Status_CreatedAt");

            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.ConfirmedAppointments)
                .HasDefaultValue(0)
                .HasColumnName("ConfirmedItems");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.GroupCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue(nameof(BookingGroupStatus.PENDING));
            entity.Property(e => e.TotalAppointments)
                .HasDefaultValue(0)
                .HasColumnName("TotalItems");
            entity.Property(e => e.CanceledAppointments)
                .HasDefaultValue(0)
                .HasColumnName("CanceledItems");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.ToTable("GroupSessions", tb =>
            {
                tb.HasCheckConstraint("CK_GroupSessions_Status", "[Status] IN ('PENDING','CONFIRMED','PARTIAL_CONFIRMED','CANCELED','COMPLETED')");
                tb.HasCheckConstraint("CK_GroupSessions_ItemCounters", "[TotalItems] >= 0 AND [ConfirmedItems] >= 0 AND [CanceledItems] >= 0 AND [ConfirmedItems] + [CanceledItems] <= [TotalItems]");
            });

            entity.HasOne(d => d.Branch).WithMany(p => p.GroupSessions)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GroupSessions_BranchId");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.GroupSessionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_GroupSessions_CreatedBy");

            entity.HasOne(d => d.Customer).WithMany(p => p.GroupSessions)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GroupSessions_CustomerId");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.GroupSessionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_GroupSessions_UpdatedBy");
        });

        modelBuilder.Entity<ApprovalRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Approval__3214EC07BCDC6900");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.RequestType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ApprovalRequestStatusCode.PENDING));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Approver).WithMany(p => p.ApprovalRequestApprovers)
                .HasForeignKey(d => d.ApproverId)
                .HasConstraintName("FK__ApprovalR__Appro__22CA2527");

            entity.HasOne(d => d.GatePass).WithMany(p => p.ApprovalRequests)
                .HasForeignKey(d => d.GatePassId)
                .HasConstraintName("FK__ApprovalR__GateP__1FEDB87C");

            entity.HasOne(d => d.MaterialReturn).WithMany(p => p.ApprovalRequests)
                .HasForeignKey(d => d.MaterialReturnId)
                .HasConstraintName("FK__ApprovalR__Mater__20E1DCB5");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.ApprovalRequestRequestedByNavigations)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ApprovalR__Reque__21D600EE");

            entity.HasOne(d => d.ServiceOrderDiscount).WithMany(p => p.ApprovalRequests)
                .HasForeignKey(d => d.ServiceOrderDiscountId)
                .HasConstraintName("FK__ApprovalR__Servi__1EF99443");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AuditLog__3214EC0770616275");

            entity.HasIndex(e => new { e.TableName, e.RecordId }, "IX_AuditLogs_TableName_RecordId");

            entity.Property(e => e.Action)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IpAddress)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SessionId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TableName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.UserType)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Branch).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK__AuditLogs__Branc__23BE4960");
        });

        modelBuilder.Entity<Bay>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Bays__3214EC07708F9CE3");

            entity.Property(e => e.BayType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ServiceArea)
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsRequired();
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.MaxTonnage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.SequenceOrder).HasDefaultValue(0);
            entity.Property(e => e.OperationalStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(BayOperationalStatusCode.ACTIVE));
            entity.Property(e => e.ReservationStatus).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.Bays)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Bays__BranchId__24B26D99");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.BayCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Bays__CreatedBy__269AB60B");

            entity.HasOne(d => d.CurrentOccupiedVehicle).WithMany(p => p.Bays)
                .HasForeignKey(d => d.CurrentOccupiedVehicleId)
                .HasConstraintName("FK__Bays__CurrentOcc__25A691D2");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.BayDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Bays__DeletedBy__2882FE7D");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.BayUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Bays__UpdatedBy__278EDA44");
        });

        modelBuilder.Entity<CapacityConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_CapacityConfigs");

            entity.Property(e => e.BookingThresholdPercent).HasColumnType("decimal(5, 4)");
            entity.Property(e => e.CreateAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdateAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany()
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK_CapacityConfigs_Branch");

            entity.HasOne(d => d.CreateByNavigation).WithMany()
                .HasForeignKey(d => d.CreateBy)
                .HasConstraintName("FK_CapacityConfigs_CreateBy");

            entity.HasOne(d => d.UpdateByNavigation).WithMany()
                .HasForeignKey(d => d.UpdateBy)
                .HasConstraintName("FK_CapacityConfigs_UpdateBy");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Branches__3214EC0712918BB8");

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Hotline)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Lat).HasColumnType("decimal(10, 8)");
            entity.Property(e => e.Lng).HasColumnType("decimal(11, 8)");
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(BranchStatusCode.ACTIVE));
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.BranchCreatedByNavigations).HasForeignKey(d => d.CreatedBy);

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.BranchDeletedByNavigations).HasForeignKey(d => d.DeletedBy);

            entity.HasOne(d => d.Manager).WithMany(p => p.BranchManagers).HasForeignKey(d => d.ManagerId);

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.BranchUpdatedByNavigations).HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<BranchException>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BranchEx__3214EC0719CB714B");

            entity.HasIndex(e => new { e.BranchId, e.ExceptionDate }, "UQ_BranchId_ExceptionDate").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsClosed).HasDefaultValue(true);
            entity.Property(e => e.Reason).HasMaxLength(255);

            entity.HasOne(d => d.ApprovedByStaff).WithMany(p => p.BranchExceptionApprovedByStaffs)
                .HasForeignKey(d => d.ApprovedByStaffId)
                .HasConstraintName("FK__BranchExc__Appro__2F2FFC0C");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchExceptions)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__BranchExc__Branc__2D47B39A");

            entity.HasOne(d => d.CreatedByStaff).WithMany(p => p.BranchExceptionCreatedByStaffs)
                .HasForeignKey(d => d.CreatedByStaffId)
                .HasConstraintName("FK__BranchExc__Creat__2E3BD7D3");
        });

        modelBuilder.Entity<BranchOperatingHour>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BranchOp__3214EC07A3A269AF");

            entity.HasIndex(e => new { e.BranchId, e.DayOfWeek }, "UQ_BranchId_DayOfWeek").IsUnique();

            entity.Property(e => e.IsDayOff).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchOperatingHours)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__BranchOpe__Branc__30242045");

            entity.HasOne(d => d.UpdatedByStaff).WithMany(p => p.BranchOperatingHours)
                .HasForeignKey(d => d.UpdatedByStaffId)
                .HasConstraintName("FK__BranchOpe__Updat__3118447E");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Categori__3214EC0799C9DC1C");

            entity.Property(e => e.CategoryName).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsDepositRequired).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CategoryCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Categorie__Creat__33008CF0");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.CategoryDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Categorie__Delet__34E8D562");

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("FK__Categorie__Paren__320C68B7");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CategoryUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Categorie__Updat__33F4B129");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ChatMess__3214EC07A1C55ACB");

            entity.HasIndex(e => e.ClientMessageId, "UX_ChatMessages_ClientMessageId")
                .IsUnique()
                .HasFilter("[ClientMessageId] IS NOT NULL");

            entity.HasIndex(e => new { e.ConversationId, e.Id }, "IX_ChatMessages_ConversationId_Id");

            entity.Property(e => e.AttachmentUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.MessageType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ChatMessageType.TEXT));
            entity.Property(e => e.SenderType)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.ToTable(tb =>
            {
                tb.HasCheckConstraint(
                    "CK_ChatMessages_SenderType",
                    $"[SenderType] IN ('{nameof(ChatSenderType.CUSTOMER)}','{nameof(ChatSenderType.STAFF)}','{nameof(ChatSenderType.SYSTEM)}')");
                tb.HasCheckConstraint(
                    "CK_ChatMessages_MessageType",
                    $"[MessageType] IN ('{nameof(ChatMessageType.TEXT)}','{nameof(ChatMessageType.IMAGE)}','{nameof(ChatMessageType.SYSTEM)}')");
            });

            entity.HasOne(d => d.Conversation).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChatMessa__Conve__35DCF99B");
        });

        modelBuilder.Entity<ChatMessageAttachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ChatMessageAttachments");

            entity.HasIndex(e => new { e.MessageId, e.SortOrder, e.Id }, "IX_ChatMessageAttachments_MessageId_SortOrder_Id");
            entity.HasIndex(e => e.CreatedAt, "IX_ChatMessageAttachments_CreatedAt");

            entity.Property(e => e.AttachmentUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.ContentType)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SortOrder).HasDefaultValue(1);

            entity.ToTable(tb =>
            {
                tb.HasCheckConstraint("CK_ChatMessageAttachments_SortOrder", "[SortOrder] >= (1)");
            });

            entity.HasOne(d => d.Message).WithMany(p => p.ChatMessageAttachments)
                .HasForeignKey(d => d.MessageId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ChatMessageAttachments_MessageId");
        });

        modelBuilder.Entity<CommissionRule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Commissi__3214EC07042111C8");

            entity.Property(e => e.CommissionType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CommissionValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.TargetType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.CommissionRules)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Commissio__Branc__3AA1AEB8");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CommissionRuleCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Commissio__Creat__37C5420D");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.CommissionRuleDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Commissio__Delet__39AD8A7F");

            entity.HasOne(d => d.StaffTier).WithMany(p => p.CommissionRules)
                .HasForeignKey(d => d.StaffTierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Commissio__Staff__36D11DD4");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CommissionRuleUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Commissio__Updat__38B96646");
        });

        modelBuilder.Entity<CommissionTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Commissi__3214EC07C2FCF5BE");

            entity.Property(e => e.AppliedCommissionType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.AppliedCommissionValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.AppliedSplitRatio)
                .HasDefaultValue(100m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.BaseAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CommissionAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.LogType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SourceType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue(nameof(CommissionTransactionStatusCode.PENDING));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.CommissionTransactionApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__Commissio__Appro__3D7E1B63");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CommissionTransactionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Commissio__Creat__3E723F9C");

            entity.HasOne(d => d.OriginalCommission).WithMany(p => p.InverseOriginalCommission)
                .HasForeignKey(d => d.OriginalCommissionId)
                .HasConstraintName("FK__Commissio__Origi__3C89F72A");

            entity.HasOne(d => d.RetailReturn).WithMany(p => p.CommissionTransactions)
                .HasForeignKey(d => d.RetailReturnId)
                .HasConstraintName("FK_CommissionTransactions_RetailReturnId");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.CommissionTransactions)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_CommissionTransactions_ServiceOrderId");

            entity.HasOne(d => d.ServiceStaffAssignment).WithMany(p => p.CommissionTransactions)
                .HasForeignKey(d => d.ServiceStaffAssignmentId)
                .HasConstraintName("FK_CommissionTransactions_ServiceStaffAssignmentId");

            entity.HasOne(d => d.Staff).WithMany(p => p.CommissionTransactionStaffs)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Commissio__Staff__3B95D2F1");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CommissionTransactionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Commissio__Updat__3F6663D5");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Conversa__3214EC073AA82895");

            entity.HasIndex(e => new { e.BranchId, e.Status, e.LastMessageAt }, "IX_Conversations_Branch_Status_LastMessageAt");
            entity.HasIndex(e => new { e.CustomerId, e.LastMessageAt }, "IX_Conversations_Customer_LastMessageAt");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.LastMessageAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ConversationStatusCode.OPEN));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();

            entity.ToTable(tb =>
            {
                tb.HasCheckConstraint(
                    "CK_Conversations_Status",
                    $"[Status] IN ('{nameof(ConversationStatus.OPEN)}','{nameof(ConversationStatus.WAITING_FOR_STAFF)}','{nameof(ConversationStatus.WAITING_FOR_CUSTOMER)}','{nameof(ConversationStatus.CLOSED)}')");
            });

            entity.HasOne(d => d.Appointment).WithMany()
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_Conversations_AppointmentId");

            entity.HasOne(d => d.AssignedStaff).WithMany()
                .HasForeignKey(d => d.AssignedStaffId)
                .HasConstraintName("FK_Conversations_AssignedStaffId");

            entity.HasOne(d => d.Branch).WithMany()
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK_Conversations_BranchId");

            entity.HasOne(d => d.Customer).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Conversat__Custo__442B18F2");

            entity.HasOne(d => d.LastMessageNavigation).WithMany()
                .HasForeignKey(d => d.LastMessageId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Conversations_LastMessageId");

            entity.HasOne(d => d.ServiceOrder).WithMany()
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_Conversations_ServiceOrderId");

            entity.HasOne(d => d.Staff).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.StaffId)
                .HasConstraintName("FK__Conversat__Staff__4336F4B9");

            entity.HasOne(d => d.Vehicle).WithMany()
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK_Conversations_VehicleId");
        });

        modelBuilder.Entity<ConversationCustomerRead>(entity =>
        {
            entity.HasKey(e => e.ConversationId).HasName("PK_ConversationCustomerReads");

            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Conversation).WithMany(p => p.ConversationCustomerReads)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ConversationCustomerReads_ConversationId");

            entity.HasOne(d => d.Customer).WithMany()
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ConversationCustomerReads_CustomerId");

            entity.HasOne(d => d.LastReadMessage).WithMany(p => p.ConversationCustomerReads)
                .HasForeignKey(d => d.LastReadMessageId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ConversationCustomerReads_LastReadMessageId");
        });

        modelBuilder.Entity<ConversationStaffRead>(entity =>
        {
            entity.HasKey(e => new { e.ConversationId, e.StaffId }).HasName("PK_ConversationStaffReads");

            entity.HasIndex(e => e.StaffId, "IX_ConversationStaffReads_StaffId");

            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Conversation).WithMany(p => p.ConversationStaffReads)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ConversationStaffReads_ConversationId");

            entity.HasOne(d => d.LastReadMessage).WithMany(p => p.ConversationStaffReads)
                .HasForeignKey(d => d.LastReadMessageId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ConversationStaffReads_LastReadMessageId");

            entity.HasOne(d => d.Staff).WithMany()
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ConversationStaffReads_StaffId");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC071C60D783");

            entity.HasIndex(e => e.Phone, "IX_Customers_Phone");

            entity.Property(e => e.AccessFailedCount).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(255);
            entity.Property(e => e.GoogleId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsLockedOut).HasDefaultValue(false);
            entity.Property(e => e.NoShowCount).HasDefaultValue(0);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SecurityStamp)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("GUEST");
            entity.Property(e => e.TotalSpending)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CustomerCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Customers__Creat__48EFCE0F");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.CustomerDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Customers__Delet__4AD81681");

            entity.HasOne(d => d.Tier).WithMany(p => p.Customers)
                .HasForeignKey(d => d.TierId)
                .HasConstraintName("FK__Customers__TierI__47FBA9D6");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CustomerUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Customers__Updat__49E3F248");
        });

        modelBuilder.Entity<CustomerFeedback>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC07F1754D23");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerFeedbacks)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CustomerF__Custo__451F3D2B");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.CustomerFeedbacks)
                .HasForeignKey(d => d.RetailOrderId)
                .HasConstraintName("FK_CustomerFeedbacks_RetailOrderId");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.CustomerFeedbacks)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_CustomerFeedbacks_ServiceOrderId");
        });

        modelBuilder.Entity<CustomerTier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC07163563D7");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DiscountPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.MinSpending).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TierName).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CustomerTierCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__CustomerT__Creat__4BCC3ABA");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.CustomerTierDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__CustomerT__Delet__4DB4832C");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CustomerTierUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__CustomerT__Updat__4CC05EF3");
        });

        modelBuilder.Entity<CustomerVehicle>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC07D8CEE2BC");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsCurrentOwner).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CustomerVehicleCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__CustomerV__Creat__5090EFD7");

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerVehicles)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CustomerV__Custo__4EA8A765");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.CustomerVehicleDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__CustomerV__Delet__52793849");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CustomerVehicleUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__CustomerV__Updat__51851410");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.CustomerVehicles)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CustomerV__Vehic__4F9CCB9E");
        });

        modelBuilder.Entity<CustomerVoucher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC0716F1AF48");

            entity.Property(e => e.IsUsed).HasDefaultValue(false);

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerVouchers)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CustomerV__Custo__536D5C82");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.CustomerVouchers)
                .HasForeignKey(d => d.RetailOrderId)
                .HasConstraintName("FK__CustomerV__Retai__5649C92D");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.CustomerVouchers)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK__CustomerV__Servi__5555A4F4");

            entity.HasOne(d => d.Voucher).WithMany(p => p.CustomerVouchers)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CustomerV__Vouch__546180BB");
        });

        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Deliveri__3214EC070B1DD92B");

            entity.Property(e => e.CarrierName).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ShippingAddress).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(DeliveryStatusCode.PENDING));
            entity.Property(e => e.TrackingCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DeliveryCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Deliverie__Creat__5832119F");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.Deliveries)
                .HasForeignKey(d => d.RetailOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Deliverie__Retai__573DED66");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DeliveryUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Deliverie__Updat__592635D8");
        });

        modelBuilder.Entity<DiagnoseRecommendation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Diagnose__3214EC070DEF315F");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ItemType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Urgency)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DiagnoseRecommendationCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__DiagnoseR__Creat__5DEAEAF5");

            entity.HasOne(d => d.Diagnose).WithMany(p => p.DiagnoseRecommendations)
                .HasForeignKey(d => d.DiagnoseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DiagnoseR__Diagn__5A1A5A11");

            entity.HasOne(d => d.InventoryItem).WithMany(p => p.DiagnoseRecommendations)
                .HasForeignKey(d => d.InventoryItemId)
                .HasConstraintName("FK__DiagnoseR__Inven__5CF6C6BC");

            entity.HasOne(d => d.ParentRecommendation).WithMany(p => p.InverseParentRecommendation)
                .HasForeignKey(d => d.ParentRecommendationId)
                .HasConstraintName("FK__DiagnoseR__Paren__5B0E7E4A");

            entity.HasOne(d => d.StandardService).WithMany(p => p.DiagnoseRecommendations)
                .HasForeignKey(d => d.StandardServiceId)
                .HasConstraintName("FK__DiagnoseR__Stand__5C02A283");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DiagnoseRecommendationUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__DiagnoseR__Updat__5EDF0F2E");
        });

        modelBuilder.Entity<Diagnosis>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Diagnose__3214EC071BC3DD6E");

            entity.Property(e => e.CancellationDecisionNote).HasMaxLength(500);
            entity.Property(e => e.CancellationReason).HasMaxLength(500);
            entity.Property(e => e.CancellationStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue(nameof(DiagnosisCancellationStatusCode.NONE));
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DispatchTag)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.DispatchCollaborationMeta).HasMaxLength(4000);
            entity.Property(e => e.Severity)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(DiagnosisStatus.PENDING));
            entity.Property(e => e.SupportRequestNote).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ApprovedByForeman).WithMany(p => p.DiagnosisApprovedByForemen)
                .HasForeignKey(d => d.ApprovedByForemanId)
                .HasConstraintName("FK__Diagnoses__Appro__61BB7BD9");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.Diagnoses)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Diagnoses__Servi__5FD33367");

            entity.HasOne(d => d.Technician).WithMany(p => p.DiagnosisTechnicians)
                .HasForeignKey(d => d.TechnicianId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Diagnoses__Techn__60C757A0");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DiagnosisUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Diagnoses__Updat__62AFA012");
        });

        modelBuilder.Entity<ExpenseLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ExpenseL__3214EC07B63E3350");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ExpenseType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProofUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ExpenseLogStatusCode.PENDING));

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.ExpenseLogApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__ExpenseLo__Appro__668030F6");

            entity.HasOne(d => d.Branch).WithMany(p => p.ExpenseLogs)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ExpenseLo__Branc__63A3C44B");

            entity.HasOne(d => d.ShiftClosing).WithMany(p => p.ExpenseLogs)
                .HasForeignKey(d => d.ShiftClosingId)
                .HasConstraintName("FK__ExpenseLo__Shift__6497E884");

            entity.HasOne(d => d.Staff).WithMany(p => p.ExpenseLogStaffs)
                .HasForeignKey(d => d.StaffId)
                .HasConstraintName("FK__ExpenseLo__Staff__658C0CBD");
        });

        modelBuilder.Entity<GatePass>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__GatePass__3214EC07516211D2");

            entity.ToTable("GatePass");

            entity.HasIndex(e => e.QrCode, "UQ__GatePass__EE32FA44C549FC8E").IsUnique();

            entity.Property(e => e.ClearanceType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsOldPartsChecked).HasDefaultValue(false);
            entity.Property(e => e.OldPartsPackingListUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.QrCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(GatePassStatusCode.ISSUED));

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.GatePassApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__GatePass__Approv__695C9DA1");

            entity.HasOne(d => d.IssuedByNavigation).WithMany(p => p.GatePassIssuedByNavigations)
                .HasForeignKey(d => d.IssuedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GatePass__Issued__68687968");

            entity.HasOne(d => d.ScannedByNavigation).WithMany(p => p.GatePassScannedByNavigations)
                .HasForeignKey(d => d.ScannedBy)
                .HasConstraintName("FK__GatePass__Scanne__6A50C1DA");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.GatePasses)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GatePass__Servic__6774552F");
        });

        modelBuilder.Entity<InsuranceCompany>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Insuranc__3214EC07749DA508");

            entity.Property(e => e.ContactPerson).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CreditLimit)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CurrentDebt)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InsuranceCompanyCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Insurance__Creat__6B44E613");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.InsuranceCompanyDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Insurance__Delet__6D2D2E85");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InsuranceCompanyUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Insurance__Updat__6C390A4C");
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Inventor__3214EC07EA32CA02");

            entity.HasIndex(e => new { e.WarehouseId, e.ProductId }, "UQ_Warehouse_Product").IsUnique();

            entity.Property(e => e.CurrentMacPrice)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("Current_MAC_Price");
            entity.Property(e => e.LocationBin)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MaxStockLevel).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.MinStockLevel)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ReservedQuantity)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");
            entity.Property(e => e.TotalInventoryValue)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.Inventories)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventori__Produ__6F1576F7");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.Inventories)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventori__Wareh__6E2152BE");
        });

        modelBuilder.Entity<InventoryAudit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Inventor__3214EC07DF0AA8BB");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(InventoryAuditStatusCode.DRAFT));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.InventoryAuditApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__Inventory__Appro__72E607DB");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InventoryAuditCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventory__Creat__73DA2C14");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InventoryAuditUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Inventory__Updat__74CE504D");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.InventoryAudits)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventory__Wareh__71F1E3A2");
        });

        modelBuilder.Entity<InventoryAuditDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Inventor__3214EC07DA0B0DAD");

            entity.Property(e => e.ActualQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.DifferenceQty)
                .HasComputedColumnSql("([ActualQty]-[SystemQty])", true)
                .HasColumnType("decimal(19, 3)");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.SystemQty).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.Audit).WithMany(p => p.InventoryAuditDetails)
                .HasForeignKey(d => d.AuditId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventory__Audit__70099B30");

            entity.HasOne(d => d.Product).WithMany(p => p.InventoryAuditDetails)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventory__Produ__70FDBF69");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Invoices__3214EC077603BEDB");

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.CompanyAddress).HasMaxLength(500);
            entity.Property(e => e.CompanyName).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.InvoiceUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.PaidAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PayerType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(InvoicePaymentStatusCode.UNPAID));
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ReceiveEmail)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SubTotalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TotalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InvoiceCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Invoices__Create__7C6F7215");

            entity.HasOne(d => d.CreditApprovedByStaff).WithMany(p => p.InvoiceCreditApprovedByStaffs)
                .HasForeignKey(d => d.CreditApprovedByStaffId)
                .HasConstraintName("FK__Invoices__Credit__7B7B4DDC");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.RetailOrderId)
                .HasConstraintName("FK__Invoices__Retail__7993056A");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK__Invoices__Servic__789EE131");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InvoiceUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Invoices__Update__7D63964E");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK__Invoices__Vehicl__7A8729A3");
        });

        modelBuilder.Entity<InvoiceDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__InvoiceD__3214EC07BA422D95");

            entity.Property(e => e.CostPriceAtTime).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountAllocatedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ItemName).HasMaxLength(255);
            entity.Property(e => e.ItemType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxRate)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.UnitPriceAtTime).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceDetails)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__InvoiceDe__Invoi__75C27486");

            entity.HasOne(d => d.Product).WithMany(p => p.InvoiceDetails)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__InvoiceDe__Produ__76B698BF");

            entity.HasOne(d => d.StandardService).WithMany(p => p.InvoiceDetails)
                .HasForeignKey(d => d.StandardServiceId)
                .HasConstraintName("FK__InvoiceDe__Stand__77AABCF8");
        });

        modelBuilder.Entity<MaterialRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Material__3214EC07FEAF631C");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsRework).HasDefaultValue(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.RequestDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(MaterialRequestStatusCode.PENDING));

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.MaterialRequests)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_MaterialRequests_ServiceOrderId");

            entity.HasOne(d => d.Storekeeper).WithMany(p => p.MaterialRequestStorekeepers)
                .HasForeignKey(d => d.StorekeeperId)
                .HasConstraintName("FK__MaterialR__Store__041093DD");

            entity.HasOne(d => d.Technician).WithMany(p => p.MaterialRequestTechnicians)
                .HasForeignKey(d => d.TechnicianId)
                .HasConstraintName("FK__MaterialR__Techn__031C6FA4");
        });

        modelBuilder.Entity<MaterialRequestDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Material__3214EC07D90DB7BF");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IssuedQuantity)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.RequestedCondition)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RequestedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ReturnedQuantity)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(MaterialRequestDetailStatusCode.PENDING));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.WastedQuantity)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MaterialRequestDetailCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__MaterialR__Creat__004002F9");

            entity.HasOne(d => d.Product).WithMany(p => p.MaterialRequestDetails)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialR__Produ__7E57BA87");

            entity.HasOne(d => d.ServiceOrderDetail).WithMany(p => p.MaterialRequestDetails)
                .HasForeignKey(d => d.ServiceOrderDetailId)
                .HasConstraintName("FK_MaterialRequestDetails_ServiceOrderDetailId");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MaterialRequestDetailUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__MaterialR__Updat__01342732");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.MaterialRequestDetails)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialR__Wareh__7F4BDEC0");
        });

        modelBuilder.Entity<MaterialReturn>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Material__3214EC07BC10D807");

            entity.HasIndex(e => e.ReturnCode, "UQ__Material__4CF726C9D2977954").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ItemCondition)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Reason).HasMaxLength(255);
            entity.Property(e => e.ReceivedByStaffId).HasColumnName("ReceivedBy_StaffId");
            entity.Property(e => e.ReturnCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ReturnUnit).HasMaxLength(50);
            entity.Property(e => e.ReturnedByStaffId).HasColumnName("ReturnedBy_StaffId");
            entity.Property(e => e.ReturnedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(MaterialReturnStatusCode.PENDING));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MaterialReturnCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__MaterialR__Creat__09C96D33");

            entity.HasOne(d => d.MaterialRequestDetail).WithMany(p => p.MaterialReturns)
                .HasForeignKey(d => d.MaterialRequestDetailId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialR__Mater__05F8DC4F");

            entity.HasOne(d => d.Product).WithMany(p => p.MaterialReturns)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialR__Produ__06ED0088");

            entity.HasOne(d => d.ReceivedByStaff).WithMany(p => p.MaterialReturnReceivedByStaffs)
                .HasForeignKey(d => d.ReceivedByStaffId)
                .HasConstraintName("FK__MaterialR__Recei__08D548FA");

            entity.HasOne(d => d.ReturnedByStaff).WithMany(p => p.MaterialReturnReturnedByStaffs)
                .HasForeignKey(d => d.ReturnedByStaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialR__Retur__07E124C1");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.MaterialReturns)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_MaterialReturns_ServiceOrderId");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MaterialReturnUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__MaterialR__Updat__0ABD916C");
        });

        modelBuilder.Entity<ModelSpecificRule>(entity =>
        {
            entity.HasKey(e => new { e.ModelId, e.ProductId }).HasName("PK__ModelSpe__23976D404AEA9849");

            entity.Property(e => e.StandardCapacity).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Tolerance)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Model).WithMany(p => p.ModelSpecificRules)
                .HasForeignKey(d => d.ModelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ModelSpec__Model__0CA5D9DE");

            entity.HasOne(d => d.Product).WithMany(p => p.ModelSpecificRules)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ModelSpec__Produ__0D99FE17");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC0731481BF1");

            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.LinkAction)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SentAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.Customer).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Notificat__Custo__0E8E2250");
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC073CB02436");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.TriggerEvent)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.NotificationTemplateCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Notificat__Creat__0F824689");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.NotificationTemplateDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Notificat__Delet__116A8EFB");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.NotificationTemplateUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Notificat__Updat__10766AC2");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Payments__3214EC07735396A5");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.PayerType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(PaymentStatusCode.COMPLETED));
            entity.Property(e => e.TransactionCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Direction)
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired()
                .HasDefaultValue("INFLOW");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Cashier).WithMany(p => p.PaymentCashiers)
                .HasForeignKey(d => d.CashierId)
                .HasConstraintName("FK__Payments__Cashie__162F4418");

            entity.HasOne(d => d.Invoice).WithMany(p => p.Payments)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("FK__Payments__Invoic__18178C8A");

            entity.HasOne(d => d.Method).WithMany(p => p.Payments)
                .HasForeignKey(d => d.MethodId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Payments__Method__19FFD4FC");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.Payments)
                .HasForeignKey(d => d.RetailOrderId)
                .HasConstraintName("FK__Payments__Retail__190BB0C3");

            entity.HasOne(d => d.ReversedPayment).WithMany(p => p.InverseReversedPayment)
                .HasForeignKey(d => d.ReversedPaymentId)
                .HasConstraintName("FK__Payments__Revers__1AF3F935");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.Payments)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK__Payments__Servic__17236851");

            entity.HasOne(d => d.ShiftClosing).WithMany(p => p.Payments)
                .HasForeignKey(d => d.ShiftClosingId)
                .HasConstraintName("FK__Payments__ShiftC__153B1FDF");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PaymentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Payments__Update__1BE81D6E");
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PaymentM__3214EC07B07F4781");

            entity.HasIndex(e => e.MethodCode, "UQ__PaymentM__11E9210D55BD757B").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.MethodCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MethodName).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PaymentMethodCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__PaymentMe__Creat__125EB334");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.PaymentMethodDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__PaymentMe__Delet__1446FBA6");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PaymentMethodUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__PaymentMe__Updat__1352D76D");
        });

        modelBuilder.Entity<PartInventoryReservation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_PartInventoryReservations");

            entity.Property(e => e.CreateAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ReservedQuantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("RESERVED");
            entity.Property(e => e.UpdateAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreateByNavigation).WithMany()
                .HasForeignKey(d => d.CreateBy)
                .HasConstraintName("FK_PartInventoryReservations_CreateBy");

            entity.HasOne(d => d.Product).WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartInventoryReservations_Product");

            entity.HasOne(d => d.UpdateByNavigation).WithMany()
                .HasForeignKey(d => d.UpdateBy)
                .HasConstraintName("FK_PartInventoryReservations_UpdateBy");

            entity.HasOne(d => d.WorkSlot).WithMany()
                .HasForeignKey(d => d.WorkSlotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartInventoryReservations_WorkSlot");
        });

        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Packages");

            entity.HasIndex(e => e.Code, "UQ_Packages_Code").IsUnique();

            entity.Property(e => e.BasePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Category).WithMany()
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Packages_ServiceCategories");
        });

        modelBuilder.Entity<PackageItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_PackageItems");

            entity.HasIndex(e => new { e.PackageId, e.StandardServiceId }, "UQ_PackageItems_Package_Service").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDefault).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Package).WithMany(p => p.PackageItems)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PackageItems_Packages");

            entity.HasOne(d => d.StandardService).WithMany()
                .HasForeignKey(d => d.StandardServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PackageItems_StandardServices");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Products__3214EC07968B91BC");

            entity.HasIndex(e => e.Sku, "UQ__Products__CA1ECF0DDDECDF2B").IsUnique();

            entity.Property(e => e.CostPrice)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsStrictCapacityControl).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.RequireEmptyReturn).HasDefaultValue(false);
            entity.Property(e => e.RetailPrice)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ServicePrice)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Sku)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("SKU");
            entity.Property(e => e.TaxRate)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.WarrantyMonths).HasDefaultValue(0);

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK__Products__Catego__1DD065E0");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProductCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Products__Create__1EC48A19");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.ProductDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Products__Delete__20ACD28B");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ProductUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Products__Update__1FB8AE52");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductI__3214EC0756CDEDF5");

            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.ImageUrl)
                .HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.IsMain).HasDefaultValue(false);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ProductIm__Produ__1CDC41A7");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Purchase__3214EC07E8647D31");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(PurchaseOrderStatusCode.PENDING));
            entity.Property(e => e.TotalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.ApprovedByStaff).WithMany(p => p.PurchaseOrderApprovedByStaffs)
                .HasForeignKey(d => d.ApprovedByStaffId)
                .HasConstraintName("FK__PurchaseO__Appro__2665ABE1");

            entity.HasOne(d => d.Branch).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Branc__23893F36");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PurchaseOrderCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__PurchaseO__Creat__2759D01A");

            entity.HasOne(d => d.DestinationWarehouse).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.DestinationWarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Desti__247D636F");

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Suppl__257187A8");
        });

        modelBuilder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Purchase__3214EC071913A1D3");

            entity.Property(e => e.CostPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OrderedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ReceivedQuantity)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.Product).WithMany(p => p.PurchaseOrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Produ__22951AFD");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.PurchaseOrderItems)
                .HasForeignKey(d => d.PurchaseOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Purch__21A0F6C4");
        });

        modelBuilder.Entity<QcInspection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__QC_Inspe__3214EC07E74328AB");

            entity.ToTable("QC_Inspections");

            entity.Property(e => e.FailReason).HasMaxLength(500);
            entity.Property(e => e.QcStaffId).HasColumnName("QC_StaffId");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.QcStaff).WithMany(p => p.QcInspectionQcStaffs)
                .HasForeignKey(d => d.QcStaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QC_Inspec__QC_St__2942188C");

            entity.HasOne(d => d.ReworkTechnician).WithMany(p => p.QcInspectionReworkTechnicians)
                .HasForeignKey(d => d.ReworkTechnicianId)
                .HasConstraintName("FK__QC_Inspec__Rewor__2B2A60FE");

            entity.HasOne(d => d.ServiceOrderDetail).WithMany(p => p.QcInspections)
                .HasForeignKey(d => d.ServiceOrderDetailId)
                .HasConstraintName("FK__QC_Inspec__Servi__2A363CC5");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.QcInspections)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QC_Inspec__Servi__284DF453");
        });

        modelBuilder.Entity<RetailOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RetailOr__3214EC07E6EE6099");

            entity.HasIndex(e => e.OrderCode, "UQ__RetailOr__999B5229199338C4").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CustomerNotes).HasMaxLength(500);
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OrderCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(RetailOrderStatus.DRAFT));
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TierDiscountAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalDiscountAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.VoucherDiscountAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Branch).WithMany(p => p.RetailOrders)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailOrd__Branc__2E06CDA9");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RetailOrderCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__RetailOrd__Creat__2FEF161B");

            entity.HasOne(d => d.Customer).WithMany(p => p.RetailOrders)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__RetailOrd__Custo__2EFAF1E2");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RetailOrderUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__RetailOrd__Updat__30E33A54");

            entity.HasOne(d => d.Voucher).WithMany(p => p.RetailOrders)
                .HasForeignKey(d => d.VoucherId)
                .HasConstraintName("FK_RetailOrders_VoucherId");
        });

        modelBuilder.Entity<RetailOrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RetailOr__3214EC0757E0441D");

            entity.Property(e => e.CostPriceAtTime).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxRate)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.UnitPriceAtTime).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.RetailOrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailOrd__Produ__2D12A970");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.RetailOrderItems)
                .HasForeignKey(d => d.RetailOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailOrd__Retai__2C1E8537");
        });

        modelBuilder.Entity<RetailReturn>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RetailRe__3214EC07D241738E");

            entity.HasIndex(e => e.ReturnCode, "UQ__RetailRe__4CF726C98A4E23DB").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.ReturnCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ReturnDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(RetailReturnStatusCode.PENDING));
            entity.Property(e => e.TotalRefundAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.RetailReturnApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__RetailRet__Appro__39788055");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RetailReturnCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailRet__Creat__3A6CA48E");

            entity.HasOne(d => d.ReceivedByNavigation).WithMany(p => p.RetailReturnReceivedByNavigations)
                .HasForeignKey(d => d.ReceivedBy)
                .HasConstraintName("FK__RetailRet__Recei__38845C1C");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.RetailReturns)
                .HasForeignKey(d => d.RetailOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailRet__Retai__379037E3");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RetailReturnUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__RetailRet__Updat__3B60C8C7");
        });

        modelBuilder.Entity<RetailReturnItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RetailRe__3214EC0700BDCE4A");

            entity.HasIndex(e => new { e.RetailReturnId, e.RetailOrderItemId }, "UQ_RetailReturnItem_Ticket").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ItemCondition)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.RefundAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ReturnedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue(nameof(RetailReturnItemStatusCode.PENDING));

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RetailReturnItemCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailRet__Creat__35A7EF71");

            entity.HasOne(d => d.RetailOrderItem).WithMany(p => p.RetailReturnItems)
                .HasForeignKey(d => d.RetailOrderItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailRet__Retai__33BFA6FF");

            entity.HasOne(d => d.RetailReturn).WithMany(p => p.RetailReturnItems)
                .HasForeignKey(d => d.RetailReturnId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailRet__Retai__32CB82C6");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RetailReturnItemUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__RetailRet__Updat__369C13AA");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.RetailReturnItems)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailRet__Wareh__34B3CB38");
        });

        modelBuilder.Entity<RevenueSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RevenueS__3214EC07FCDD558C");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.NetCashFlow)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NetProfit)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalCollected)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalDebt)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalExpense)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalRevenue)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Branch).WithMany(p => p.RevenueSnapshots)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RevenueSn__Branc__3C54ED00");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Roles__3214EC079FD50E25");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.RoleCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RoleName).HasMaxLength(100);
            entity.Property(e => e.Scope)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RoleCreatedByNavigations).HasForeignKey(d => d.CreatedBy);

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.RoleDeletedByNavigations).HasForeignKey(d => d.DeletedBy);

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RoleUpdatedByNavigations).HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<ServiceCategory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceC__3214EC07586F8C98");

            entity.HasIndex(e => e.Code, "UQ__ServiceC__A25C5AA76FAA12AA").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ServiceCategoryCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__ServiceCa__Creat__40257DE4");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.ServiceCategoryDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__ServiceCa__Delet__420DC656");

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory)
                .HasForeignKey(d => d.ParentCategoryId)
                .HasConstraintName("FK__ServiceCa__Paren__4301EA8F");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ServiceCategoryUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__ServiceCa__Updat__4119A21D");
        });

        modelBuilder.Entity<ServiceOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceO__3214EC07A6BCD573");

            entity.HasIndex(e => e.GroupSessionId, "IX_ServiceOrders_GroupSessionId");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_ServiceOrders_Status_CreatedAt");

            entity.HasIndex(e => e.Code, "UQ__ServiceO__A25C5AA7FBF72344").IsUnique();

            entity.Property(e => e.CancellationReason).HasMaxLength(500);
            entity.Property(e => e.ArrivalTime).HasColumnType("datetime2");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DepositForfeitedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DepositRefundedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DepositStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderDepositStatusCode.NOT_REQUIRED));
            entity.Property(e => e.FinalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceClaimMode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.InsuranceDeductibleAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceDeductibleOption)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.InsuranceQuoteState)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.OldPartsReturnOption)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OrderCategory)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderPaymentStatusCode.UNPAID));
            entity.Property(e => e.QcinspectorId).HasColumnName("QCInspectorId");
            entity.Property(e => e.Qcstatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderQcStatusCode.PENDING))
                .HasColumnName("QCStatus");
            entity.Property(e => e.QcverifiedAt).HasColumnName("QCVerifiedAt");
            entity.Property(e => e.RequiredDepositAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RequestedPackageIds).HasMaxLength(500);
            entity.Property(e => e.RevisionNumber).HasDefaultValue(1);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderLifecycleStatus.NEW));
            entity.Property(e => e.SubTotalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalDepositedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalDiscount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.ToTable("ServiceOrders", tb =>
            {
                tb.HasCheckConstraint("CK_ServiceOrders_Status", "[Status] IN ('NEW','CHECKED_IN','WAITING','DIAGNOSING','QUOTING','WAITING_APPROVAL','APPROVED','IN_PROGRESS','REPAIRING','READY','WAITING_PAYMENT','CLOSED','CANCELED')");
                tb.HasCheckConstraint("CK_ServiceOrders_OrderCategory", "[OrderCategory] IS NULL OR ([OrderCategory]='WARRANTLY' OR [OrderCategory]='REWORK' OR [OrderCategory]='INSURANCE_REPAIR' OR [OrderCategory]='QUICK_SERVICE' OR [OrderCategory]='GENERAL_REPAIR' OR [OrderCategory]='MAINTENANCE')");
            });

            entity.HasOne(d => d.Appointment).WithMany(p => p.ServiceOrders)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK__ServiceOr__Appoi__53385258");

            entity.HasOne(d => d.GroupSession).WithMany(p => p.ServiceOrders)
                .HasForeignKey(d => d.GroupSessionId)
                .HasConstraintName("FK_ServiceOrders_GroupSessionId");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ServiceOrderCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__ServiceOr__Creat__59E54FE7");

            entity.HasOne(d => d.Customer).WithMany(p => p.ServiceOrders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Custo__515009E6");

            entity.HasOne(d => d.ExecuteBranch).WithMany(p => p.ServiceOrderExecuteBranches)
                .HasForeignKey(d => d.ExecuteBranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Execu__55209ACA");

            entity.HasOne(d => d.OriginBranch).WithMany(p => p.ServiceOrderOriginBranches)
                .HasForeignKey(d => d.OriginBranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Origi__542C7691");

            entity.HasOne(d => d.OriginalServiceOrder).WithMany(p => p.InverseOriginalServiceOrder)
                .HasForeignKey(d => d.OriginalServiceOrderId)
                .HasConstraintName("FK__ServiceOr__Origi__5614BF03");

            entity.HasOne(d => d.Qcinspector).WithMany(p => p.ServiceOrderQcinspectors)
                .HasForeignKey(d => d.QcinspectorId)
                .HasConstraintName("FK__ServiceOr__QCIns__58F12BAE");

            entity.HasOne(d => d.Receptionist).WithMany(p => p.ServiceOrderReceptionists)
                .HasForeignKey(d => d.ReceptionistId)
                .HasConstraintName("FK__ServiceOr__Recep__57FD0775");

            entity.HasOne(d => d.ServiceAdvisor).WithMany(p => p.ServiceOrderServiceAdvisors)
                .HasForeignKey(d => d.ServiceAdvisorId)
                .HasConstraintName("FK__ServiceOr__Servi__5708E33C");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ServiceOrderUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__ServiceOr__Updat__5AD97420");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.ServiceOrders)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Vehic__52442E1F");
        });

        modelBuilder.Entity<ServiceOrderDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceO__3214EC07F58DFBE0");

            entity.Property(e => e.BuyoutCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CostPriceAtTime).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CustomerCoPayAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountAllocatedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountValue)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.EstimatedMinutes);
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GarageAbsorbedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceApprovedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceRequestedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderDetailInsuranceStatusCode.NOT_APPLICABLE));
            entity.Property(e => e.IsCustomerBroughtPart).HasDefaultValue(false);
            entity.Property(e => e.IsReturnOldPart).HasDefaultValue(false);
            entity.Property(e => e.IsWarrantyItem).HasDefaultValue(false);
            entity.Property(e => e.IssuedQuantity)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ItemName).HasMaxLength(255);
            entity.Property(e => e.ItemType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.PayerType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderDetailStatusCode.DRAFT));
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxRate).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.UnitPriceAtTime).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.AbsorbedApprovedByNavigation).WithMany(p => p.ServiceOrderDetailAbsorbedApprovedByNavigations)
                .HasForeignKey(d => d.AbsorbedApprovedBy)
                .HasConstraintName("FK__ServiceOr__Absor__43F60EC8");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ServiceOrderDetailCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__ServiceOr__Creat__44EA3301");

            entity.HasOne(d => d.DiagnoseRecommendation).WithMany(p => p.ServiceOrderDetails)
                .HasForeignKey(d => d.DiagnoseRecommendationId)
                .HasConstraintName("FK__ServiceOr__Diagn__47C69FAC");

            entity.HasOne(d => d.InsuranceCompany).WithMany(p => p.ServiceOrderDetails)
                .HasForeignKey(d => d.InsuranceCompanyId)
                .HasConstraintName("FK__ServiceOr__Insur__4C8B54C9");

            entity.HasOne(d => d.OriginalOrderDetail).WithMany(p => p.InverseOriginalOrderDetail)
                .HasForeignKey(d => d.OriginalOrderDetailId)
                .HasConstraintName("FK__ServiceOr__Origi__4AA30C57");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.ServiceOrderDetails)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Servi__46D27B73");

            entity.HasOne(d => d.StandardService).WithMany(p => p.ServiceOrderDetails)
                .HasForeignKey(d => d.StandardServiceId)
                .HasConstraintName("FK__ServiceOr__Stand__48BAC3E5");

            entity.HasOne(d => d.Supplier).WithMany(p => p.ServiceOrderDetails)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK__ServiceOr__Suppl__4B973090");

            entity.HasOne(d => d.Technician).WithMany(p => p.ServiceOrderDetailTechnicians)
                .HasForeignKey(d => d.TechnicianId)
                .HasConstraintName("FK__ServiceOr__Techn__49AEE81E");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ServiceOrderDetailUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__ServiceOr__Updat__45DE573A");
        });

        modelBuilder.Entity<ServiceOrderDiscount>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceO__3214EC07B07AEE3B");

            entity.HasIndex(e => new { e.ServiceOrderId, e.DiscountCategory }, "UQ_ServiceOrder_DiscountCategory").IsUnique();

            entity.Property(e => e.AppliedAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountCategory).HasComputedColumnSql("(case when [DiscountType]='MEMBERSHIP' then (1) when [DiscountType]='MANUAL' OR [DiscountType]='VOUCHER' then (2)  end)", true);
            entity.Property(e => e.DiscountType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ValueType)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.AppliedByNavigation).WithMany(p => p.ServiceOrderDiscounts)
                .HasForeignKey(d => d.AppliedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Appli__4F67C174");

            entity.HasOne(d => d.CustomerTier).WithMany(p => p.ServiceOrderDiscounts)
                .HasForeignKey(d => d.CustomerTierId)
                .HasConstraintName("FK__ServiceOr__Custo__4E739D3B");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.ServiceOrderDiscounts)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Servi__4D7F7902");

            entity.HasOne(d => d.Voucher).WithMany(p => p.ServiceOrderDiscounts)
                .HasForeignKey(d => d.VoucherId)
                .HasConstraintName("FK_ServiceOrderDiscounts_VoucherId");
        });

        modelBuilder.Entity<ServiceOrderInsuranceAsset>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ServiceOrderInsuranceAssets");

            entity.HasIndex(e => new { e.ServiceOrderId, e.AssetType, e.SortOrder }, "IX_ServiceOrderInsuranceAssets_ServiceOrder_Type_Order");

            entity.Property(e => e.AssetType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.InsuranceAssets)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ServiceOrderInsuranceAssets_ServiceOrders");
        });

        modelBuilder.Entity<ServiceOrderInsuranceUpload>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ServiceOrderInsuranceUploads");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_ServiceOrderInsuranceUploads_Status_CreatedAt");

            entity.Property(e => e.AssetType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.BlobPath)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.InsuranceUploads)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_ServiceOrderInsuranceUploads_ServiceOrders");
        });

        modelBuilder.Entity<ServiceOrderSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceO__3214EC0711723175");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ServiceOrderSnapshots)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__ServiceOr__Creat__5CC1BC92");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.ServiceOrderSnapshots)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Servi__5BCD9859");
        });

        modelBuilder.Entity<ServiceRefund>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceR__3214EC07E092AD2D");

            entity.Property(e => e.ApprovalNote).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.RefundAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceRefundStatusCode.PENDING));

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.ServiceRefundApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__ServiceRe__Appro__5EAA0504");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ServiceRefundCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceRe__Creat__5F9E293D");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.ServiceRefunds)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceRe__Servi__5DB5E0CB");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ServiceRefundUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__ServiceRe__Updat__60924D76");
        });

        modelBuilder.Entity<ServiceStaffAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceS__3214EC074773E670");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.EstimatedCommission).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsRework).HasDefaultValue(false);
            entity.Property(e => e.RoleInTask)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SplitRatio)
                .HasDefaultValue(100.00m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceProgressStatus.PENDING));

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.ServiceStaffAssignmentAssignedByNavigations)
                .HasForeignKey(d => d.AssignedBy)
                .HasConstraintName("FK__ServiceSt__Assig__627A95E8");

            entity.HasOne(d => d.ServiceOrderDetail).WithMany(p => p.ServiceStaffAssignments)
                .HasForeignKey(d => d.ServiceOrderDetailId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceSt__Servi__618671AF");

            entity.HasOne(d => d.Technician).WithMany(p => p.ServiceStaffAssignmentTechnicians)
                .HasForeignKey(d => d.TechnicianId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceSt__Techn__636EBA21");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ServiceStaffAssignmentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__ServiceSt__Updat__6462DE5A");
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Shifts__3214EC0704926738");

            entity.Property(e => e.ShiftName).HasMaxLength(100);

            entity.HasOne(d => d.Branch).WithMany(p => p.Shifts)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Shifts__BranchId__69279377");
        });

        modelBuilder.Entity<ShiftClosing>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ShiftClo__3214EC07117EA13A");

            entity.Property(e => e.ActualBalance)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Discrepancy)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ExpectedBalance)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.OpeningBalance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ShiftClosingStatusCode.OPEN));
            entity.Property(e => e.TotalCashIn)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalCashOut)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Branch).WithMany(p => p.ShiftClosings)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ShiftClos__Branc__65570293");

            entity.HasOne(d => d.Cashier).WithMany(p => p.ShiftClosingCashiers)
                .HasForeignKey(d => d.CashierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ShiftClos__Cashi__673F4B05");

            entity.HasOne(d => d.Manager).WithMany(p => p.ShiftClosingManagers)
                .HasForeignKey(d => d.ManagerId)
                .HasConstraintName("FK__ShiftClos__Manag__68336F3E");

            entity.HasOne(d => d.WorkShift).WithMany(p => p.ShiftClosings)
                .HasForeignKey(d => d.WorkShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ShiftClos__WorkS__664B26CC");
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Teams");
            entity.ToTable("Teams");

            entity.HasIndex(e => new { e.BranchId, e.Code }, "UQ_Teams_BranchId_Code")
                .IsUnique()
                .HasFilter("[Code] IS NOT NULL");

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.SpecialtyTag)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(TeamStatusCode.ACTIVE));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.Teams)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Teams_BranchId");

            entity.HasOne(d => d.Leader).WithMany()
                .HasForeignKey(d => d.LeaderId)
                .HasConstraintName("FK_Teams_LeaderId");
        });

        modelBuilder.Entity<Staff>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Staffs__3214EC07674AA95B");

            entity.Property(e => e.AccessFailedCount).HasDefaultValue(0);
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CitizenId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(255);
            entity.Property(e => e.GoogleId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsLockedOut).HasDefaultValue(false);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SecurityStamp)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(StaffStatusCode.ACTIVE));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.Staff)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK__Staffs__BranchId__6B0FDBE9");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InverseCreatedByNavigation)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Staffs__CreatedB__6CF8245B");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.InverseDeletedByNavigation)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Staffs__DeletedB__6EE06CCD");

            entity.HasOne(d => d.Role).WithMany(p => p.Staff)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__Staffs__RoleId__6A1BB7B0");

            entity.HasOne(d => d.StaffTier).WithMany(p => p.Staff)
                .HasForeignKey(d => d.StaffTierId)
                .HasConstraintName("FK__Staffs__StaffTie__6C040022");

            entity.HasOne(d => d.Team).WithMany(p => p.Staff)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("FK_Staffs_TeamId");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InverseUpdatedByNavigation)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Staffs__UpdatedB__6DEC4894");
        });

        modelBuilder.Entity<StaffSchedule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StaffSch__3214EC0779258F4D");

            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Shift).WithMany(p => p.StaffSchedules)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StaffSche__Shift__70C8B53F");

            entity.HasOne(d => d.Staff).WithMany(p => p.StaffSchedules)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StaffSche__Staff__6FD49106");
        });

        modelBuilder.Entity<StaffTier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StaffTie__3214EC077A692A60");

            entity.Property(e => e.BaseSalary)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.TierName).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StaffTierCreatedByNavigations).HasForeignKey(d => d.CreatedBy);

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.StaffTierDeletedByNavigations).HasForeignKey(d => d.DeletedBy);

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.StaffTierUpdatedByNavigations).HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<StandardService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Standard__3214EC0745B61617");

            entity.HasIndex(e => e.Code, "UQ__Standard__A25C5AA70956C7EB").IsUnique();

            entity.Property(e => e.BasePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ConsumableCost)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.TaxRate)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.WarrantyKm).HasDefaultValue(0);
            entity.Property(e => e.WarrantyMonths).HasDefaultValue(0);

            entity.HasOne(d => d.Category).WithMany(p => p.StandardServices)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StandardS__Categ__74994623");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StandardServiceCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__StandardS__Creat__758D6A5C");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.StandardServiceDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__StandardS__Delet__7775B2CE");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.StandardServiceUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__StandardS__Updat__76818E95");
        });

        modelBuilder.Entity<ServiceBom>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ServiceBOMs");

            entity.ToTable("ServiceBOMs");

            entity.Property(e => e.CreateAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsMandatory).HasDefaultValue(true);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.UpdateAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreateByNavigation).WithMany()
                .HasForeignKey(d => d.CreateBy)
                .HasConstraintName("FK_ServiceBOMs_CreateBy");

            entity.HasOne(d => d.Product).WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ServiceBOMs_Product");

            entity.HasOne(d => d.Service).WithMany()
                .HasForeignKey(d => d.ServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ServiceBOMs_Service");

            entity.HasOne(d => d.UpdateByNavigation).WithMany()
                .HasForeignKey(d => d.UpdateBy)
                .HasConstraintName("FK_ServiceBOMs_UpdateBy");

            entity.HasOne(d => d.VehicleModel).WithMany()
                .HasForeignKey(d => d.VehicleModelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ServiceBOMs_VehicleModel");
        });

        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockTra__3214EC07104DE41B");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Type)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__StockTran__Creat__795DFB40");

            entity.HasOne(d => d.MaterialReturn).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.MaterialReturnId)
                .HasConstraintName("FK_StockTransactions_MaterialReturnId");

            entity.HasOne(d => d.Product).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StockTran__Produ__7869D707");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.PurchaseOrderId)
                .HasConstraintName("FK_StockTransactions_PurchaseOrderId");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.RetailOrderId)
                .HasConstraintName("FK_StockTransactions_RetailOrderId");

            entity.HasOne(d => d.RetailReturn).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.RetailReturnId)
                .HasConstraintName("FK_StockTransactions_RetailReturnId");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Supplier__3214EC077AD2834E");

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.ContactName).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CurrentDebt)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SupplierName).HasMaxLength(255);
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SupplierCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Suppliers__Creat__7E22B05D");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.SupplierDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Suppliers__Delet__000AF8CF");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SupplierUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Suppliers__Updat__7F16D496");
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Vehicles__3214EC0730F8DFC4");

            entity.HasIndex(e => e.LicensePlate, "UQ_Vehicles_LicensePlate")
                .IsUnique()
                .HasFilter("([LicensePlate] IS NOT NULL)");

            entity.HasIndex(e => e.Vin, "UQ_Vehicles_VIN")
                .IsUnique()
                .HasFilter("([VIN] IS NOT NULL)");

            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.LicensePlate)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Odo)
                .HasDefaultValue(0)
                .HasColumnName("ODO");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Vin)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("VIN");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.VehicleCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Vehicles__Create__0D64F3ED");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.VehicleDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Vehicles__Delete__0F4D3C5F");

            entity.HasOne(d => d.Model).WithMany(p => p.Vehicles)
                .HasForeignKey(d => d.ModelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Vehicles__ModelI__0C70CFB4");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.VehicleUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Vehicles__Update__0E591826");
        });

        modelBuilder.Entity<VehicleBrand>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__VehicleB__3214EC07EADA13FE");

            entity.HasIndex(e => e.Code, "UQ__VehicleB__A25C5AA7E9C6A8B3").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CountryOfOrigin).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.LogoUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.VehicleBrandCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__VehicleBr__Creat__00FF1D08");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.VehicleBrandDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__VehicleBr__Delet__02E7657A");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.VehicleBrandUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__VehicleBr__Updat__01F34141");
        });

        modelBuilder.Entity<VehicleConditionLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__VehicleC__3214EC075745CAC4");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CustomerSignatureUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.DashboardWarningLights).HasMaxLength(500);
            entity.Property(e => e.LogType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Valuables).HasMaxLength(1000);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.VehicleConditionLogCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__VehicleCo__Creat__05C3D225");

            entity.HasOne(d => d.Inspector).WithMany(p => p.VehicleConditionLogInspectors)
                .HasForeignKey(d => d.InspectorId)
                .HasConstraintName("FK__VehicleCo__Inspe__04CFADEC");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.VehicleConditionLogs)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__VehicleCo__Servi__03DB89B3");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.VehicleConditionLogUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<VehicleInsurance>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__VehicleI__3214EC07269DD274");

            entity.Property(e => e.PolicyNumber)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.InsuranceCompany).WithMany(p => p.VehicleInsurances)
                .HasForeignKey(d => d.InsuranceCompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__VehicleIn__Insur__07AC1A97");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.VehicleInsurances)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__VehicleIn__Vehic__06B7F65E");
        });

        modelBuilder.Entity<VehicleModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__VehicleM__3214EC0789BEED56");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CylinderCapacity).HasMaxLength(50);
            entity.Property(e => e.FuelType).HasMaxLength(50);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.ModelName).HasMaxLength(100);
            entity.Property(e => e.Segment).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Brand).WithMany(p => p.VehicleModels)
                .HasForeignKey(d => d.BrandId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__VehicleMo__Brand__08A03ED0");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.VehicleModelCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__VehicleMo__Creat__09946309");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.VehicleModelDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__VehicleMo__Delet__0B7CAB7B");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.VehicleModelUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__VehicleMo__Updat__0A888742");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Vouchers__3214EC070B87BA0F");

            entity.HasIndex(e => e.Code, "UQ__Vouchers__A25C5AA7E33C4EC6").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsStackable).HasDefaultValue(false);
            entity.Property(e => e.MaxDiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinOrderValue)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UsageLimit).HasDefaultValue(0);
            entity.Property(e => e.UsedCount).HasDefaultValue(0);
            entity.Property(e => e.Value).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.VoucherCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Vouchers__Create__10416098");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.VoucherDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Vouchers__Delete__1229A90A");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.VoucherUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Vouchers__Update__113584D1");
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Warehous__3214EC07FE923BC8");

            entity.HasIndex(e => e.Code, "UQ__Warehous__A25C5AA73F13B012").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.Warehouses)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Warehouse__Branc__131DCD43");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.WarehouseCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Warehouse__Creat__150615B5");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.WarehouseDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Warehouse__Delet__16EE5E27");

            entity.HasOne(d => d.Manager).WithMany(p => p.WarehouseManagers)
                .HasForeignKey(d => d.ManagerId)
                .HasConstraintName("FK__Warehouse__Manag__1411F17C");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.WarehouseUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Warehouse__Updat__15FA39EE");
        });

        modelBuilder.Entity<WorkSlot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__WorkSlot__3214EC0774196CAC");

            entity.Property(e => e.IsAdditional).HasDefaultValue(false);
            entity.Property(e => e.SequenceOrder).HasDefaultValue(1);
            entity.Property(e => e.SlotType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(WorkSlotStatus.RESERVED));

            entity.HasOne(d => d.Appointment).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_WorkSlots_AppointmentId");

            entity.HasOne(d => d.Bay).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.BayId)
                .HasConstraintName("FK__WorkSlots__BayId__19CACAD2");

            entity.HasOne(d => d.Branch).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__WorkSlots__Branc__18D6A699");

            entity.HasOne(d => d.Customer).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__WorkSlots__Custo__1BB31344");

            entity.HasOne(d => d.ParentSlot).WithMany(p => p.InverseParentSlot)
                .HasForeignKey(d => d.ParentSlotId)
                .HasConstraintName("FK__WorkSlots__Paren__17E28260");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_WorkSlots_ServiceOrderId");

            entity.HasOne(d => d.Technician).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.TechnicianId)
                .HasConstraintName("FK__WorkSlots__Techn__1ABEEF0B");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK__WorkSlots__Vehic__1CA7377D");
        });

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
        var user = _httpContextAccessor.HttpContext?.User;
        var userIdStr = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? user?.FindFirst("sub")?.Value
                        ?? user?.FindFirst("id")?.Value
                        ?? user?.FindFirst("uid")?.Value;

        if (!long.TryParse(userIdStr, out var parsedId))
        {
            return null;
        }

        var userExistsInTrackedStaff = ChangeTracker.Entries<Staff>().Any(e => e.Entity.Id == parsedId);
        var userExistsInDatabase = userExistsInTrackedStaff || Staffs.AsNoTracking().Any(s => s.Id == parsedId);
        return userExistsInDatabase ? parsedId : null;
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

