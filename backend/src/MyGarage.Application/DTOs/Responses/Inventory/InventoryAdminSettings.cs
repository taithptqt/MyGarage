namespace MyGarage.DTOs.Responses;

public sealed record InventoryAdminSettings
{
    public Dictionary<string, long> DefaultWarehouseByItemType { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, long> DefaultWarehouseByOperation { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<long, string> ProductClassifications { get; init; } = new();
    public List<string> UnitOptions { get; init; } = [];
    public InventoryPolicyRules PolicyRules { get; init; } = new();
    public DateTime? UpdatedAt { get; init; }

    public static InventoryAdminSettings CreateDefault() => new()
    {
        DefaultWarehouseByItemType = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["SELLABLE"] = 0,
            ["DEFECTIVE"] = 0,
            ["CONSUMABLE"] = 0,
            ["TRANSIT"] = 0
        },
        DefaultWarehouseByOperation = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["PURCHASE_RECEIVE"] = 0,
            ["SERVICE_ISSUE"] = 0,
            ["RETAIL_ISSUE"] = 0,
            ["MATERIAL_RETURN"] = 0,
            ["TRANSFER"] = 0
        },
        UnitOptions = ["Cai", "Bo", "Hop", "Lit", "Kg"],
        PolicyRules = new InventoryPolicyRules
        {
            AllowNegativeStock = false,
            LowStockThresholdPercent = 15,
            ReserveRule = "STRICT",
            AllowOverIssue = false,
            MaterialReturnRule = "ALLOW_WITH_REASON",
            TransferRule = "REQUIRE_APPROVAL"
        },
        UpdatedAt = VietnamTime.Now
    };
}

public sealed record InventoryPolicyRules
{
    public bool AllowNegativeStock { get; init; }
    public decimal LowStockThresholdPercent { get; init; }
    public string ReserveRule { get; init; } = "STRICT";
    public bool AllowOverIssue { get; init; }
    public string MaterialReturnRule { get; init; } = "ALLOW_WITH_REASON";
    public string TransferRule { get; init; } = "REQUIRE_APPROVAL";
    public Dictionary<long, decimal> SafeStockByBranch { get; init; } = new();
}
