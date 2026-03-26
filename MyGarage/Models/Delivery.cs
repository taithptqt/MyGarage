using MyGarage.Interfaces;

using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Delivery : IAuditEntity
{
    public long Id { get; set; }

    public long RetailOrderId { get; set; }

    public string ShippingAddress { get; set; } = null!;

    public string? CarrierName { get; set; }

    public string? TrackingCode { get; set; }

    public string? DeliveryEvidenceUrls { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual RetailOrder RetailOrder { get; set; } = null!;

    public virtual Staff? UpdatedByNavigation { get; set; }
}
