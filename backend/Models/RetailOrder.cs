using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class RetailOrder
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public string OrderCode { get; set; } = null!;

    public long? CustomerId { get; set; }

    public decimal SubTotal { get; set; }

    public decimal? TierDiscountAmount { get; set; }

    public long? VoucherId { get; set; }

    public decimal? VoucherDiscountAmount { get; set; }

    public decimal? TotalDiscountAmount { get; set; }

    public decimal? TaxAmount { get; set; }

    public decimal FinalAmount { get; set; }

    public string? Status { get; set; }

    public string? CustomerNotes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual ICollection<CustomerFeedback> CustomerFeedbacks { get; set; } = new List<CustomerFeedback>();

    public virtual ICollection<CustomerVoucher> CustomerVouchers { get; set; } = new List<CustomerVoucher>();

    public virtual ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<RetailOrderItem> RetailOrderItems { get; set; } = new List<RetailOrderItem>();

    public virtual ICollection<RetailReturn> RetailReturns { get; set; } = new List<RetailReturn>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual Voucher? Voucher { get; set; }

    public virtual ICollection<VoucherApprovalRequest> VoucherApprovalRequests { get; set; } = new List<VoucherApprovalRequest>();

    public virtual ICollection<VoucherRedemption> VoucherRedemptions { get; set; } = new List<VoucherRedemption>();
}
