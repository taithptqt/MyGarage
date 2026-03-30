using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Branch
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public decimal? Lat { get; set; }

    public decimal? Lng { get; set; }

    public string? Hotline { get; set; }

    public long? ManagerId { get; set; }

    public string? Email { get; set; }

    public string? Status { get; set; }

    public string? TaxCode { get; set; }

    public int? MaxWorkSlots { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual ICollection<AccountingPeriod> AccountingPeriods { get; set; } = new List<AccountingPeriod>();

    public virtual ICollection<AccountsPayable> AccountsPayables { get; set; } = new List<AccountsPayable>();

    public virtual ICollection<AiSmartDispatching> AiSmartDispatchings { get; set; } = new List<AiSmartDispatching>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();

    public virtual ICollection<Bay> Bays { get; set; } = new List<Bay>();

    public virtual ICollection<BranchException> BranchExceptions { get; set; } = new List<BranchException>();

    public virtual ICollection<BranchOperatingHour> BranchOperatingHours { get; set; } = new List<BranchOperatingHour>();

    public virtual ICollection<CommissionRule> CommissionRules { get; set; } = new List<CommissionRule>();

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<ExpenseLog> ExpenseLogs { get; set; } = new List<ExpenseLog>();

    public virtual ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();

    public virtual Staff? Manager { get; set; }

    public virtual ICollection<PayrollRun> PayrollRuns { get; set; } = new List<PayrollRun>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<RetailOrder> RetailOrders { get; set; } = new List<RetailOrder>();

    public virtual ICollection<RevenueSnapshot> RevenueSnapshots { get; set; } = new List<RevenueSnapshot>();

    public virtual ICollection<ServiceOrder> ServiceOrderExecuteBranches { get; set; } = new List<ServiceOrder>();

    public virtual ICollection<ServiceOrder> ServiceOrderOriginBranches { get; set; } = new List<ServiceOrder>();

    public virtual ICollection<ShiftClosing> ShiftClosings { get; set; } = new List<ShiftClosing>();

    public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();

    public virtual ICollection<Staff> Staff { get; set; } = new List<Staff>();

    public virtual ICollection<StaffSalaryProfile> StaffSalaryProfiles { get; set; } = new List<StaffSalaryProfile>();

    public virtual ICollection<SupplierPayment> SupplierPayments { get; set; } = new List<SupplierPayment>();

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual ICollection<VoucherApprovalRequest> VoucherApprovalRequests { get; set; } = new List<VoucherApprovalRequest>();

    public virtual ICollection<VoucherAuditLog> VoucherAuditLogs { get; set; } = new List<VoucherAuditLog>();

    public virtual ICollection<VoucherRedemption> VoucherRedemptions { get; set; } = new List<VoucherRedemption>();

    public virtual ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();

    public virtual ICollection<WorkSlot> WorkSlots { get; set; } = new List<WorkSlot>();
}
