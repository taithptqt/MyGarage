using MyGarage.Interfaces;

using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class CommissionTransaction : IAuditEntity
{
    public long Id { get; set; }

    public long StaffId { get; set; }

    public long? ServiceOrderId { get; set; }

    public long? ServiceStaffAssignmentId { get; set; }

    public long? RetailOrderId { get; set; }

    public long? CommissionRuleId { get; set; }

    public string SourceType { get; set; } = null!;

    public decimal BaseAmount { get; set; }

    public string AppliedCommissionType { get; set; } = null!;

    public decimal AppliedCommissionValue { get; set; }

    public decimal? AppliedSplitRatio { get; set; }

    public decimal CommissionAmount { get; set; }

    public string LogType { get; set; } = null!;

    public long? OriginalCommissionId { get; set; }

    public long? ServiceRefundId { get; set; }

    public long? RetailReturnId { get; set; }

    public string? Status { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual ICollection<CommissionTransaction> InverseOriginalCommission { get; set; } = new List<CommissionTransaction>();

    public virtual CommissionTransaction? OriginalCommission { get; set; }

    public virtual RetailReturn? RetailReturn { get; set; }

    public virtual ServiceOrder? ServiceOrder { get; set; }

    public virtual ServiceStaffAssignment? ServiceStaffAssignment { get; set; }

    public virtual Staff Staff { get; set; } = null!;

    public virtual Staff? UpdatedByNavigation { get; set; }
}
