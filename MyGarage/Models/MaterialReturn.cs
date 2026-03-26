using MyGarage.Interfaces;

using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class MaterialReturn : IAuditEntity
{
    public long Id { get; set; }

    public string ReturnCode { get; set; } = null!;

    public long? ServiceOrderId { get; set; }

    public long MaterialRequestDetailId { get; set; }

    public long ProductId { get; set; }

    public string? ReturnUnit { get; set; }

    public decimal ReturnedQuantity { get; set; }

    public string ItemCondition { get; set; } = null!;

    public long ReturnedByStaffId { get; set; }

    public long? ReceivedByStaffId { get; set; }

    public DateTime? ReturnTime { get; set; }

    public string? Status { get; set; }

    public string? Reason { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual ICollection<ApprovalRequest> ApprovalRequests { get; set; } = new List<ApprovalRequest>();

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual MaterialRequestDetail MaterialRequestDetail { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual Staff? ReceivedByStaff { get; set; }

    public virtual Staff ReturnedByStaff { get; set; } = null!;

    public virtual ServiceOrder? ServiceOrder { get; set; }

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual Staff? UpdatedByNavigation { get; set; }
}
