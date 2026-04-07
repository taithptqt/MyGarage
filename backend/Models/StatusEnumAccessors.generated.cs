using System.ComponentModel.DataAnnotations.Schema;
using MyGarage.Enums;

namespace MyGarage.Models;

public partial class AccountingPeriod
{
    [NotMapped]
    public AccountingPeriodStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<AccountingPeriodStatusCode>(Status, AccountingPeriodStatusCode.OPEN);
        set => Status = value.ToString();
    }

}

public partial class AccountsPayable
{
    [NotMapped]
    public AccountsPayableStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<AccountsPayableStatusCode>(Status, AccountsPayableStatusCode.OPEN);
        set => Status = value.ToString();
    }

}

public partial class AiFraudMonitor
{
    [NotMapped]
    public AiFraudMonitorStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<AiFraudMonitorStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class AiSmartDispatching
{
    [NotMapped]
    public AiDispatchStatus? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<AiDispatchStatus>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class Appointment
{
    [NotMapped]
    public AppointmentStatus StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<AppointmentStatus>(Status, AppointmentStatus.PENDING);
        set => Status = value.ToString();
    }

}

public partial class ApprovalRequest
{
    [NotMapped]
    public ApprovalRequestStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<ApprovalRequestStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class AttendanceAdjustment
{
    [NotMapped]
    public AttendanceAdjustmentStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<AttendanceAdjustmentStatusCode>(Status, AttendanceAdjustmentStatusCode.PENDING);
        set => Status = value.ToString();
    }

}

public partial class BankReconciliation
{
    [NotMapped]
    public BankReconciliationStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<BankReconciliationStatusCode>(Status, BankReconciliationStatusCode.DRAFT);
        set => Status = value.ToString();
    }

}

public partial class BankTransaction
{
    [NotMapped]
    public BankTransactionStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<BankTransactionStatusCode>(Status, BankTransactionStatusCode.UNMATCHED);
        set => Status = value.ToString();
    }

}

public partial class Bay
{
    [NotMapped]
    public BayOperationalStatusCode? OperationalStatusEnum
    {
        get => StatusEnumParser.ParseNullable<BayOperationalStatusCode>(OperationalStatus);
        set => OperationalStatus = StatusEnumParser.ToDbValue(value);
    }

}

public partial class Branch
{
    [NotMapped]
    public BranchStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<BranchStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class CommissionTransaction
{
    [NotMapped]
    public CommissionTransactionStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<CommissionTransactionStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class Conversation
{
    [NotMapped]
    public ConversationStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<ConversationStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class Delivery
{
    [NotMapped]
    public DeliveryStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<DeliveryStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class Diagnosis
{
    [NotMapped]
    public DiagnosisCancellationStatusCode? CancellationStatusEnum
    {
        get => StatusEnumParser.ParseNullable<DiagnosisCancellationStatusCode>(CancellationStatus);
        set => CancellationStatus = StatusEnumParser.ToDbValue(value);
    }

    [NotMapped]
    public DiagnosisStatus? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<DiagnosisStatus>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class EmployeeLoan
{
    [NotMapped]
    public EmployeeLoanStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<EmployeeLoanStatusCode>(Status, EmployeeLoanStatusCode.ACTIVE);
        set => Status = value.ToString();
    }

}

public partial class ExpenseLog
{
    [NotMapped]
    public ExpenseLogStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<ExpenseLogStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class FinanceApprovalRequest
{
    [NotMapped]
    public FinanceApprovalRequestStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<FinanceApprovalRequestStatusCode>(Status, FinanceApprovalRequestStatusCode.PENDING);
        set => Status = value.ToString();
    }

}

public partial class GatePass
{
    [NotMapped]
    public GatePassStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<GatePassStatusCode>(Status, GatePassStatusCode.ISSUED);
        set => Status = value.ToString();
    }

}

public partial class GroupSession
{
    [NotMapped]
    public BookingGroupStatus StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<BookingGroupStatus>(Status, BookingGroupStatus.PENDING);
        set => Status = value.ToString();
    }

}

public partial class InventoryAudit
{
    [NotMapped]
    public InventoryAuditStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<InventoryAuditStatusCode>(Status, InventoryAuditStatusCode.DRAFT);
        set => Status = value.ToString();
    }

}

public partial class Invoice
{
    [NotMapped]
    public InvoicePaymentStatusCode? PaymentStatusEnum
    {
        get => StatusEnumParser.ParseNullable<InvoicePaymentStatusCode>(PaymentStatus);
        set => PaymentStatus = StatusEnumParser.ToDbValue(value);
    }

}

public partial class JournalEntry
{
    [NotMapped]
    public JournalEntryStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<JournalEntryStatusCode>(Status, JournalEntryStatusCode.DRAFT);
        set => Status = value.ToString();
    }

}

public partial class MaterialRequest
{
    [NotMapped]
    public MaterialRequestStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<MaterialRequestStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class MaterialRequestDetail
{
    [NotMapped]
    public MaterialRequestDetailStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<MaterialRequestDetailStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class MaterialReturn
{
    [NotMapped]
    public MaterialReturnStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<MaterialReturnStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class OvertimeEntry
{
    [NotMapped]
    public OvertimeEntryStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<OvertimeEntryStatusCode>(Status, OvertimeEntryStatusCode.PENDING);
        set => Status = value.ToString();
    }

}

public partial class Payment
{
    [NotMapped]
    public PaymentStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<PaymentStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class PayrollItem
{
    [NotMapped]
    public PayrollItemStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<PayrollItemStatusCode>(Status, PayrollItemStatusCode.DRAFT);
        set => Status = value.ToString();
    }

}

public partial class PayrollPeriod
{
    [NotMapped]
    public PayrollPeriodStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<PayrollPeriodStatusCode>(Status, PayrollPeriodStatusCode.OPEN);
        set => Status = value.ToString();
    }

}

public partial class PayrollRun
{
    [NotMapped]
    public PayrollRunStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<PayrollRunStatusCode>(Status, PayrollRunStatusCode.DRAFT);
        set => Status = value.ToString();
    }

}

public partial class PurchaseOrder
{
    [NotMapped]
    public PurchaseOrderStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<PurchaseOrderStatusCode>(Status, PurchaseOrderStatusCode.PENDING);
        set => Status = value.ToString();
    }

}

public partial class QcInspection
{
    [NotMapped]
    public QcInspectionStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<QcInspectionStatusCode>(Status, QcInspectionStatusCode.FAIL);
        set => Status = value.ToString();
    }

}

public partial class RetailOrder
{
    [NotMapped]
    public RetailOrderStatus? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<RetailOrderStatus>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class RetailReturn
{
    [NotMapped]
    public RetailReturnStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<RetailReturnStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class RetailReturnItem
{
    [NotMapped]
    public RetailReturnItemStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<RetailReturnItemStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class SalaryAdvance
{
    [NotMapped]
    public SalaryAdvanceStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<SalaryAdvanceStatusCode>(Status, SalaryAdvanceStatusCode.PENDING);
        set => Status = value.ToString();
    }

}

public partial class ServiceOrder
{
    [NotMapped]
    public ServiceOrderDepositStatusCode? DepositStatusEnum
    {
        get => StatusEnumParser.ParseNullable<ServiceOrderDepositStatusCode>(DepositStatus);
        set => DepositStatus = StatusEnumParser.ToDbValue(value);
    }

    [NotMapped]
    public ServiceOrderPaymentStatusCode? PaymentStatusEnum
    {
        get => StatusEnumParser.ParseNullable<ServiceOrderPaymentStatusCode>(PaymentStatus);
        set => PaymentStatus = StatusEnumParser.ToDbValue(value);
    }

    [NotMapped]
    public ServiceOrderQcStatusCode? QcStatusEnum
    {
        get => StatusEnumParser.ParseNullable<ServiceOrderQcStatusCode>(Qcstatus);
        set => Qcstatus = StatusEnumParser.ToDbValue(value);
    }

    [NotMapped]
    public ServiceOrderLifecycleStatus StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<ServiceOrderLifecycleStatus>(Status, ServiceOrderLifecycleStatus.NEW);
        set => Status = value.ToString();
    }

}

public partial class ServiceOrderDetail
{
    [NotMapped]
    public ServiceOrderDetailInsuranceStatusCode? InsuranceStatusEnum
    {
        get => StatusEnumParser.ParseNullable<ServiceOrderDetailInsuranceStatusCode>(InsuranceStatus);
        set => InsuranceStatus = StatusEnumParser.ToDbValue(value);
    }

    [NotMapped]
    public ServiceOrderDetailStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<ServiceOrderDetailStatusCode>(Status, ServiceOrderDetailStatusCode.DRAFT);
        set => Status = value.ToString();
    }

}

public partial class ServiceRefund
{
    [NotMapped]
    public ServiceRefundStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<ServiceRefundStatusCode>(Status, ServiceRefundStatusCode.PENDING);
        set => Status = value.ToString();
    }

}

public partial class ServiceStaffAssignment
{
    [NotMapped]
    public ServiceProgressStatus? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<ServiceProgressStatus>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class ShiftClosing
{
    [NotMapped]
    public ShiftClosingStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<ShiftClosingStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class Staff
{
    [NotMapped]
    public StaffStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<StaffStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class StaffContract
{
    [NotMapped]
    public StaffContractStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<StaffContractStatusCode>(Status, StaffContractStatusCode.ACTIVE);
        set => Status = value.ToString();
    }

}

public partial class StaffSalaryProfile
{
    [NotMapped]
    public StaffSalaryProfileStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<StaffSalaryProfileStatusCode>(Status, StaffSalaryProfileStatusCode.ACTIVE);
        set => Status = value.ToString();
    }

}

public partial class StaffSchedule
{
    [NotMapped]
    public StaffScheduleStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<StaffScheduleStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class SupplierInvoice
{
    [NotMapped]
    public SupplierInvoiceStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<SupplierInvoiceStatusCode>(Status, SupplierInvoiceStatusCode.DRAFT);
        set => Status = value.ToString();
    }

}

public partial class TaxFiling
{
    [NotMapped]
    public TaxFilingStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<TaxFilingStatusCode>(Status, TaxFilingStatusCode.DRAFT);
        set => Status = value.ToString();
    }

}

public partial class Team
{
    [NotMapped]
    public TeamStatusCode? StatusEnum
    {
        get => StatusEnumParser.ParseNullable<TeamStatusCode>(Status);
        set => Status = StatusEnumParser.ToDbValue(value);
    }

}

public partial class Voucher
{
    [NotMapped]
    public VoucherStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<VoucherStatusCode>(Status, VoucherStatusCode.DRAFT);
        set => Status = value.ToString();
    }

}

public partial class VoucherApprovalRequest
{
    [NotMapped]
    public VoucherApprovalRequestStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<VoucherApprovalRequestStatusCode>(Status, VoucherApprovalRequestStatusCode.PENDING);
        set => Status = value.ToString();
    }

}

public partial class VoucherCampaign
{
    [NotMapped]
    public VoucherCampaignStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<VoucherCampaignStatusCode>(Status, VoucherCampaignStatusCode.DRAFT);
        set => Status = value.ToString();
    }

}

public partial class VoucherCodeBatch
{
    [NotMapped]
    public VoucherCodeBatchStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<VoucherCodeBatchStatusCode>(Status, VoucherCodeBatchStatusCode.COMPLETED);
        set => Status = value.ToString();
    }

}

public partial class VoucherRedemption
{
    [NotMapped]
    public VoucherRedemptionStatusCode RedemptionStatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<VoucherRedemptionStatusCode>(RedemptionStatus, VoucherRedemptionStatusCode.REDEEMED);
        set => RedemptionStatus = value.ToString();
    }

    [NotMapped]
    public VoucherRedemptionValidationStatusCode ValidationStatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<VoucherRedemptionValidationStatusCode>(ValidationStatus, VoucherRedemptionValidationStatusCode.VALIDATED);
        set => ValidationStatus = value.ToString();
    }

}

public partial class VwPayrollRunSummary
{
    [NotMapped]
    public VwPayrollRunSummaryStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<VwPayrollRunSummaryStatusCode>(Status, VwPayrollRunSummaryStatusCode.DRAFT);
        set => Status = value.ToString();
    }

}

public partial class VwSupplierPayableSummary
{
    [NotMapped]
    public SupplierInvoiceStatusCode StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<SupplierInvoiceStatusCode>(Status, SupplierInvoiceStatusCode.DRAFT);
        set => Status = value.ToString();
    }

}

public partial class WorkSlot
{
    [NotMapped]
    public WorkSlotStatus StatusEnum
    {
        get => StatusEnumParser.ParseOrDefault<WorkSlotStatus>(Status, WorkSlotStatus.AVAILABLE);
        set => Status = value.ToString();
    }

}


