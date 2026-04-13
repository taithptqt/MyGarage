namespace MyGarage.DTOs.Responses;

public class ServiceBomResponseDto
{
    public long Id { get; set; }
    public long ServiceId { get; set; }
    public long VehicleModelId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
}

