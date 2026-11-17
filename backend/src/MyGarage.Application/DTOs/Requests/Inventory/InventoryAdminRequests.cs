namespace MyGarage.DTOs.Requests;

public sealed record DecisionPatchDto
{
    public string? Decision { get; init; }
    public long? ApproverId { get; init; }
    public string? Note { get; init; }
}

public sealed record ResolveStockAlertRequestDto
{
    public long PurchaseOrderId { get; init; }
}
