namespace MyGarage.DTOs.OData;

public sealed class ServiceOrderODataDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public long VehicleId { get; set; }
    public string? LicensePlate { get; set; }
    public long? AppointmentId { get; set; }
    public long ExecuteBranchId { get; set; }
    public string? BranchName { get; set; }
    public string? OrderCategory { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PaymentStatus { get; set; }
    public decimal? SubTotalAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? FinalAmount { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? EstimatedHandover { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed class QuotationODataDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public long ServiceOrderId { get; set; }
    public string? ServiceOrderCode { get; set; }
    public long ExecuteBranchId { get; set; }
    public long CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string QuotationType { get; set; } = string.Empty;
    public int VersionNo { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TechnicalApprovalStatus { get; set; }
    public string? FinancialApprovalStatus { get; set; }
    public string? CustomerDecisionStatus { get; set; }
    public decimal SubTotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal TotalInsuranceAmount { get; set; }
    public decimal TotalCustomerAmount { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed class AuditLogODataDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string UserType { get; set; } = string.Empty;
    public long? BranchId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public long RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime? Timestamp { get; set; }
}

public sealed class VehicleODataDto
{
    public long Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string? Vin { get; set; }
    public long ModelId { get; set; }
    public string? ModelName { get; set; }
    public string? Color { get; set; }
    public int? Odo { get; set; }
    public int? ManufactureYear { get; set; }
    public bool? IsDeleted { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed class AppointmentODataDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string? BranchName { get; set; }
    public long CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public long? VehicleId { get; set; }
    public string? LicensePlate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CustomerNotes { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed class InventoryODataDto
{
    public long Id { get; set; }
    public long WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public long BranchId { get; set; }
    public long ProductId { get; set; }
    public string? ProductSku { get; set; }
    public string? ProductName { get; set; }
    public decimal Quantity { get; set; }
    public decimal? ReservedQuantity { get; set; }
    public decimal? MinStockLevel { get; set; }
    public decimal? MaxStockLevel { get; set; }
    public decimal? CurrentMacPrice { get; set; }
    public decimal? TotalInventoryValue { get; set; }
    public string? LocationBin { get; set; }
}

public sealed class StaffODataDto
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }
    public long? RoleId { get; set; }
    public string? RoleCode { get; set; }
    public string? RoleName { get; set; }
    public string? Status { get; set; }
    public bool? IsDeleted { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed class CustomerODataDto
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Type { get; set; } = string.Empty;
    public long? TierId { get; set; }
    public decimal? TotalSpending { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsDeleted { get; set; }
    public DateTime? CreatedAt { get; set; }
}
