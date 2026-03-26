using System;
using System.Collections.Generic;

using MyGarage.Interfaces;

namespace MyGarage.Models;

public partial class Product : IAuditEntity
{
    public long Id { get; set; }

    public string Sku { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public long? CategoryId { get; set; }

    public decimal? CostPrice { get; set; }

    public decimal? RetailPrice { get; set; }

    public decimal? ServicePrice { get; set; }

    public decimal? TaxRate { get; set; }

    public int? WarrantyMonths { get; set; }

    public bool? IsStrictCapacityControl { get; set; }

    public bool? RequireEmptyReturn { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual Category? Category { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<DiagnoseRecommendation> DiagnoseRecommendations { get; set; } = new List<DiagnoseRecommendation>();

    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

    public virtual ICollection<InventoryAuditDetail> InventoryAuditDetails { get; set; } = new List<InventoryAuditDetail>();

    public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();

    public virtual ICollection<MaterialRequestDetail> MaterialRequestDetails { get; set; } = new List<MaterialRequestDetail>();

    public virtual ICollection<MaterialReturn> MaterialReturns { get; set; } = new List<MaterialReturn>();

    public virtual ICollection<ModelSpecificRule> ModelSpecificRules { get; set; } = new List<ModelSpecificRule>();

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();

    public virtual ICollection<RetailOrderItem> RetailOrderItems { get; set; } = new List<RetailOrderItem>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual Staff? UpdatedByNavigation { get; set; }
}
