// Mục đích: Entity EF liên kết khách hàng với voucher và tham chiếu sử dụng liên quan.
using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class CustomerVoucher
{
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public long VoucherId { get; set; }

    public bool? IsUsed { get; set; }

    public DateTime? UsedAt { get; set; }

    public long? ServiceOrderId { get; set; }

    public long? RetailOrderId { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual RetailOrder? RetailOrder { get; set; }

    public virtual ServiceOrder? ServiceOrder { get; set; }

    public virtual Voucher Voucher { get; set; } = null!;
}
