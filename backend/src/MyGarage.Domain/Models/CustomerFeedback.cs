using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class CustomerFeedback
{
    public long Id { get; set; }

    public long? ServiceOrderId { get; set; }

    public long? RetailOrderId { get; set; }

    public long CustomerId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public string? ImageUrls { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual RetailOrder? RetailOrder { get; set; }

    public virtual ServiceOrder? ServiceOrder { get; set; }
}
