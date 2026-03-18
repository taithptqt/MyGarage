using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Staff
{
    public long Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = null!;

    public long? RoleId { get; set; }

    public long? BranchId { get; set; }

    public long? StaffTierId { get; set; }

    public int? SkillLevel { get; set; }

    public string? CitizenId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public DateOnly? HireDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public string? AvatarUrl { get; set; }

    public string? SecurityStamp { get; set; }

    public int? AccessFailedCount { get; set; }

    public bool? IsLockedOut { get; set; }

    public DateTime? LockoutEnd { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual ICollection<AiContentStore> AiContentStores { get; set; } = new List<AiContentStore>();

    public virtual ICollection<AiTechnicalAssistant> AiTechnicalAssistants { get; set; } = new List<AiTechnicalAssistant>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<ApprovalRequest> ApprovalRequestApprovers { get; set; } = new List<ApprovalRequest>();

    public virtual ICollection<ApprovalRequest> ApprovalRequestRequestedByNavigations { get; set; } = new List<ApprovalRequest>();

    public virtual ICollection<Bay> BayCreatedByNavigations { get; set; } = new List<Bay>();

    public virtual ICollection<Bay> BayDeletedByNavigations { get; set; } = new List<Bay>();

    public virtual ICollection<Bay> BayUpdatedByNavigations { get; set; } = new List<Bay>();

    public virtual Branch? Branch { get; set; }

    public virtual ICollection<Branch> BranchCreatedByNavigations { get; set; } = new List<Branch>();

    public virtual ICollection<Branch> BranchDeletedByNavigations { get; set; } = new List<Branch>();

    public virtual ICollection<BranchException> BranchExceptionApprovedByStaffs { get; set; } = new List<BranchException>();

    public virtual ICollection<BranchException> BranchExceptionCreatedByStaffs { get; set; } = new List<BranchException>();

    public virtual ICollection<Branch> BranchManagers { get; set; } = new List<Branch>();

    public virtual ICollection<BranchOperatingHour> BranchOperatingHours { get; set; } = new List<BranchOperatingHour>();

    public virtual ICollection<Branch> BranchUpdatedByNavigations { get; set; } = new List<Branch>();

    public virtual ICollection<Category> CategoryCreatedByNavigations { get; set; } = new List<Category>();

    public virtual ICollection<Category> CategoryDeletedByNavigations { get; set; } = new List<Category>();

    public virtual ICollection<Category> CategoryUpdatedByNavigations { get; set; } = new List<Category>();

    public virtual ICollection<CommissionRule> CommissionRuleCreatedByNavigations { get; set; } = new List<CommissionRule>();

    public virtual ICollection<CommissionRule> CommissionRuleDeletedByNavigations { get; set; } = new List<CommissionRule>();

    public virtual ICollection<CommissionRule> CommissionRuleUpdatedByNavigations { get; set; } = new List<CommissionRule>();

    public virtual ICollection<CommissionTransaction> CommissionTransactionApprovedByNavigations { get; set; } = new List<CommissionTransaction>();

    public virtual ICollection<CommissionTransaction> CommissionTransactionCreatedByNavigations { get; set; } = new List<CommissionTransaction>();

    public virtual ICollection<CommissionTransaction> CommissionTransactionStaffs { get; set; } = new List<CommissionTransaction>();

    public virtual ICollection<CommissionTransaction> CommissionTransactionUpdatedByNavigations { get; set; } = new List<CommissionTransaction>();

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual ICollection<Customer> CustomerCreatedByNavigations { get; set; } = new List<Customer>();

    public virtual ICollection<Customer> CustomerDeletedByNavigations { get; set; } = new List<Customer>();

    public virtual ICollection<CustomerTier> CustomerTierCreatedByNavigations { get; set; } = new List<CustomerTier>();

    public virtual ICollection<CustomerTier> CustomerTierDeletedByNavigations { get; set; } = new List<CustomerTier>();

    public virtual ICollection<CustomerTier> CustomerTierUpdatedByNavigations { get; set; } = new List<CustomerTier>();

    public virtual ICollection<Customer> CustomerUpdatedByNavigations { get; set; } = new List<Customer>();

    public virtual ICollection<CustomerVehicle> CustomerVehicleCreatedByNavigations { get; set; } = new List<CustomerVehicle>();

    public virtual ICollection<CustomerVehicle> CustomerVehicleDeletedByNavigations { get; set; } = new List<CustomerVehicle>();

    public virtual ICollection<CustomerVehicle> CustomerVehicleUpdatedByNavigations { get; set; } = new List<CustomerVehicle>();

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<Delivery> DeliveryCreatedByNavigations { get; set; } = new List<Delivery>();

    public virtual ICollection<Delivery> DeliveryUpdatedByNavigations { get; set; } = new List<Delivery>();

    public virtual ICollection<DiagnoseRecommendation> DiagnoseRecommendationCreatedByNavigations { get; set; } = new List<DiagnoseRecommendation>();

    public virtual ICollection<DiagnoseRecommendation> DiagnoseRecommendationUpdatedByNavigations { get; set; } = new List<DiagnoseRecommendation>();

    public virtual ICollection<Diagnosis> DiagnosisApprovedByForemen { get; set; } = new List<Diagnosis>();

    public virtual ICollection<Diagnosis> DiagnosisTechnicians { get; set; } = new List<Diagnosis>();

    public virtual ICollection<Diagnosis> DiagnosisUpdatedByNavigations { get; set; } = new List<Diagnosis>();

    public virtual ICollection<ExpenseLog> ExpenseLogApprovedByNavigations { get; set; } = new List<ExpenseLog>();

    public virtual ICollection<ExpenseLog> ExpenseLogStaffs { get; set; } = new List<ExpenseLog>();

    public virtual ICollection<GatePass> GatePassApprovedByNavigations { get; set; } = new List<GatePass>();

    public virtual ICollection<GatePass> GatePassIssuedByNavigations { get; set; } = new List<GatePass>();

    public virtual ICollection<GatePass> GatePassScannedByNavigations { get; set; } = new List<GatePass>();

    public virtual ICollection<InsuranceCompany> InsuranceCompanyCreatedByNavigations { get; set; } = new List<InsuranceCompany>();

    public virtual ICollection<InsuranceCompany> InsuranceCompanyDeletedByNavigations { get; set; } = new List<InsuranceCompany>();

    public virtual ICollection<InsuranceCompany> InsuranceCompanyUpdatedByNavigations { get; set; } = new List<InsuranceCompany>();

    public virtual ICollection<InventoryAudit> InventoryAuditApprovedByNavigations { get; set; } = new List<InventoryAudit>();

    public virtual ICollection<InventoryAudit> InventoryAuditCreatedByNavigations { get; set; } = new List<InventoryAudit>();

    public virtual ICollection<InventoryAudit> InventoryAuditUpdatedByNavigations { get; set; } = new List<InventoryAudit>();

    public virtual ICollection<Staff> InverseCreatedByNavigation { get; set; } = new List<Staff>();

    public virtual ICollection<Staff> InverseDeletedByNavigation { get; set; } = new List<Staff>();

    public virtual ICollection<Staff> InverseUpdatedByNavigation { get; set; } = new List<Staff>();

    public virtual ICollection<Invoice> InvoiceCreatedByNavigations { get; set; } = new List<Invoice>();

    public virtual ICollection<Invoice> InvoiceCreditApprovedByStaffs { get; set; } = new List<Invoice>();

    public virtual ICollection<Invoice> InvoiceUpdatedByNavigations { get; set; } = new List<Invoice>();

    public virtual ICollection<MaterialRequestDetail> MaterialRequestDetailCreatedByNavigations { get; set; } = new List<MaterialRequestDetail>();

    public virtual ICollection<MaterialRequestDetail> MaterialRequestDetailUpdatedByNavigations { get; set; } = new List<MaterialRequestDetail>();

    public virtual ICollection<MaterialRequest> MaterialRequestStorekeepers { get; set; } = new List<MaterialRequest>();

    public virtual ICollection<MaterialRequest> MaterialRequestTechnicians { get; set; } = new List<MaterialRequest>();

    public virtual ICollection<MaterialReturn> MaterialReturnCreatedByNavigations { get; set; } = new List<MaterialReturn>();

    public virtual ICollection<MaterialReturn> MaterialReturnReceivedByStaffs { get; set; } = new List<MaterialReturn>();

    public virtual ICollection<MaterialReturn> MaterialReturnReturnedByStaffs { get; set; } = new List<MaterialReturn>();

    public virtual ICollection<MaterialReturn> MaterialReturnUpdatedByNavigations { get; set; } = new List<MaterialReturn>();

    public virtual ICollection<NotificationTemplate> NotificationTemplateCreatedByNavigations { get; set; } = new List<NotificationTemplate>();

    public virtual ICollection<NotificationTemplate> NotificationTemplateDeletedByNavigations { get; set; } = new List<NotificationTemplate>();

    public virtual ICollection<NotificationTemplate> NotificationTemplateUpdatedByNavigations { get; set; } = new List<NotificationTemplate>();

    public virtual ICollection<Payment> PaymentCashiers { get; set; } = new List<Payment>();

    public virtual ICollection<PaymentMethod> PaymentMethodCreatedByNavigations { get; set; } = new List<PaymentMethod>();

    public virtual ICollection<PaymentMethod> PaymentMethodDeletedByNavigations { get; set; } = new List<PaymentMethod>();

    public virtual ICollection<PaymentMethod> PaymentMethodUpdatedByNavigations { get; set; } = new List<PaymentMethod>();

    public virtual ICollection<Payment> PaymentUpdatedByNavigations { get; set; } = new List<Payment>();

    public virtual ICollection<Product> ProductCreatedByNavigations { get; set; } = new List<Product>();

    public virtual ICollection<Product> ProductDeletedByNavigations { get; set; } = new List<Product>();

    public virtual ICollection<Product> ProductUpdatedByNavigations { get; set; } = new List<Product>();

    public virtual ICollection<PurchaseOrder> PurchaseOrderApprovedByStaffs { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<PurchaseOrder> PurchaseOrderCreatedByNavigations { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<QcInspection> QcInspectionQcStaffs { get; set; } = new List<QcInspection>();

    public virtual ICollection<QcInspection> QcInspectionReworkTechnicians { get; set; } = new List<QcInspection>();

    public virtual ICollection<RetailOrder> RetailOrderCreatedByNavigations { get; set; } = new List<RetailOrder>();

    public virtual ICollection<RetailOrder> RetailOrderUpdatedByNavigations { get; set; } = new List<RetailOrder>();

    public virtual ICollection<RetailReturn> RetailReturnApprovedByNavigations { get; set; } = new List<RetailReturn>();

    public virtual ICollection<RetailReturn> RetailReturnCreatedByNavigations { get; set; } = new List<RetailReturn>();

    public virtual ICollection<RetailReturnItem> RetailReturnItemCreatedByNavigations { get; set; } = new List<RetailReturnItem>();

    public virtual ICollection<RetailReturnItem> RetailReturnItemUpdatedByNavigations { get; set; } = new List<RetailReturnItem>();

    public virtual ICollection<RetailReturn> RetailReturnReceivedByNavigations { get; set; } = new List<RetailReturn>();

    public virtual ICollection<RetailReturn> RetailReturnUpdatedByNavigations { get; set; } = new List<RetailReturn>();

    public virtual Role? Role { get; set; }

    public virtual ICollection<Role> RoleCreatedByNavigations { get; set; } = new List<Role>();

    public virtual ICollection<Role> RoleDeletedByNavigations { get; set; } = new List<Role>();

    public virtual ICollection<Role> RoleUpdatedByNavigations { get; set; } = new List<Role>();

    public virtual ICollection<ServiceCategory> ServiceCategoryCreatedByNavigations { get; set; } = new List<ServiceCategory>();

    public virtual ICollection<ServiceCategory> ServiceCategoryDeletedByNavigations { get; set; } = new List<ServiceCategory>();

    public virtual ICollection<ServiceCategory> ServiceCategoryUpdatedByNavigations { get; set; } = new List<ServiceCategory>();

    public virtual ICollection<ServiceOrder> ServiceOrderCreatedByNavigations { get; set; } = new List<ServiceOrder>();

    public virtual ICollection<ServiceOrderDetail> ServiceOrderDetailAbsorbedApprovedByNavigations { get; set; } = new List<ServiceOrderDetail>();

    public virtual ICollection<ServiceOrderDetail> ServiceOrderDetailCreatedByNavigations { get; set; } = new List<ServiceOrderDetail>();

    public virtual ICollection<ServiceOrderDetail> ServiceOrderDetailTechnicians { get; set; } = new List<ServiceOrderDetail>();

    public virtual ICollection<ServiceOrderDetail> ServiceOrderDetailUpdatedByNavigations { get; set; } = new List<ServiceOrderDetail>();

    public virtual ICollection<ServiceOrderDiscount> ServiceOrderDiscounts { get; set; } = new List<ServiceOrderDiscount>();

    public virtual ICollection<ServiceOrder> ServiceOrderQcinspectors { get; set; } = new List<ServiceOrder>();

    public virtual ICollection<ServiceOrder> ServiceOrderReceptionists { get; set; } = new List<ServiceOrder>();

    public virtual ICollection<ServiceOrder> ServiceOrderServiceAdvisors { get; set; } = new List<ServiceOrder>();

    public virtual ICollection<ServiceOrderSnapshot> ServiceOrderSnapshots { get; set; } = new List<ServiceOrderSnapshot>();

    public virtual ICollection<ServiceOrder> ServiceOrderUpdatedByNavigations { get; set; } = new List<ServiceOrder>();

    public virtual ICollection<ServiceRefund> ServiceRefundApprovedByNavigations { get; set; } = new List<ServiceRefund>();

    public virtual ICollection<ServiceRefund> ServiceRefundCreatedByNavigations { get; set; } = new List<ServiceRefund>();

    public virtual ICollection<ServiceRefund> ServiceRefundUpdatedByNavigations { get; set; } = new List<ServiceRefund>();

    public virtual ICollection<ServiceStaffAssignment> ServiceStaffAssignmentAssignedByNavigations { get; set; } = new List<ServiceStaffAssignment>();

    public virtual ICollection<ServiceStaffAssignment> ServiceStaffAssignmentTechnicians { get; set; } = new List<ServiceStaffAssignment>();

    public virtual ICollection<ServiceStaffAssignment> ServiceStaffAssignmentUpdatedByNavigations { get; set; } = new List<ServiceStaffAssignment>();

    public virtual ICollection<ShiftClosing> ShiftClosingCashiers { get; set; } = new List<ShiftClosing>();

    public virtual ICollection<ShiftClosing> ShiftClosingManagers { get; set; } = new List<ShiftClosing>();

    public virtual ICollection<StaffSchedule> StaffSchedules { get; set; } = new List<StaffSchedule>();

    public virtual StaffTier? StaffTier { get; set; }

    public virtual ICollection<StaffTier> StaffTierCreatedByNavigations { get; set; } = new List<StaffTier>();

    public virtual ICollection<StaffTier> StaffTierDeletedByNavigations { get; set; } = new List<StaffTier>();

    public virtual ICollection<StaffTier> StaffTierUpdatedByNavigations { get; set; } = new List<StaffTier>();

    public virtual ICollection<StandardService> StandardServiceCreatedByNavigations { get; set; } = new List<StandardService>();

    public virtual ICollection<StandardService> StandardServiceDeletedByNavigations { get; set; } = new List<StandardService>();

    public virtual ICollection<StandardService> StandardServiceUpdatedByNavigations { get; set; } = new List<StandardService>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual ICollection<Supplier> SupplierCreatedByNavigations { get; set; } = new List<Supplier>();

    public virtual ICollection<Supplier> SupplierDeletedByNavigations { get; set; } = new List<Supplier>();

    public virtual ICollection<Supplier> SupplierUpdatedByNavigations { get; set; } = new List<Supplier>();

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual ICollection<VehicleBrand> VehicleBrandCreatedByNavigations { get; set; } = new List<VehicleBrand>();

    public virtual ICollection<VehicleBrand> VehicleBrandDeletedByNavigations { get; set; } = new List<VehicleBrand>();

    public virtual ICollection<VehicleBrand> VehicleBrandUpdatedByNavigations { get; set; } = new List<VehicleBrand>();

    public virtual ICollection<VehicleConditionLog> VehicleConditionLogCreatedByNavigations { get; set; } = new List<VehicleConditionLog>();

    public virtual ICollection<VehicleConditionLog> VehicleConditionLogInspectors { get; set; } = new List<VehicleConditionLog>();

    public virtual ICollection<Vehicle> VehicleCreatedByNavigations { get; set; } = new List<Vehicle>();

    public virtual ICollection<Vehicle> VehicleDeletedByNavigations { get; set; } = new List<Vehicle>();

    public virtual ICollection<VehicleModel> VehicleModelCreatedByNavigations { get; set; } = new List<VehicleModel>();

    public virtual ICollection<VehicleModel> VehicleModelDeletedByNavigations { get; set; } = new List<VehicleModel>();

    public virtual ICollection<VehicleModel> VehicleModelUpdatedByNavigations { get; set; } = new List<VehicleModel>();

    public virtual ICollection<Vehicle> VehicleUpdatedByNavigations { get; set; } = new List<Vehicle>();

    public virtual ICollection<Voucher> VoucherCreatedByNavigations { get; set; } = new List<Voucher>();

    public virtual ICollection<Voucher> VoucherDeletedByNavigations { get; set; } = new List<Voucher>();

    public virtual ICollection<Voucher> VoucherUpdatedByNavigations { get; set; } = new List<Voucher>();

    public virtual ICollection<Warehouse> WarehouseCreatedByNavigations { get; set; } = new List<Warehouse>();

    public virtual ICollection<Warehouse> WarehouseDeletedByNavigations { get; set; } = new List<Warehouse>();

    public virtual ICollection<Warehouse> WarehouseManagers { get; set; } = new List<Warehouse>();

    public virtual ICollection<Warehouse> WarehouseUpdatedByNavigations { get; set; } = new List<Warehouse>();

    public virtual ICollection<WorkSlot> WorkSlots { get; set; } = new List<WorkSlot>();
}
