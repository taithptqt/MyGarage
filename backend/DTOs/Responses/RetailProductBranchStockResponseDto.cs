using System.Collections.Generic;

namespace MyGarage.DTOs.Responses;

public class RetailBranchStockRowDto
{
    public long BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public int AvailableQuantity { get; set; }
}

public class RetailProductBranchStockResponseDto
{
    public long ProductId { get; set; }

    public List<RetailBranchStockRowDto> Branches { get; set; } = new();
}
