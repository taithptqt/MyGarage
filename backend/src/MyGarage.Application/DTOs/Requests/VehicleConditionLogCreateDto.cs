using System;
namespace MyGarage.DTOs.Requests
{
    public class VehicleConditionLogCreateDto
    {
        public long ServiceOrderId { get; set; }
        public string? LogType { get; set; }
        public int? OdoReading { get; set; }
        public byte? FuelLevel { get; set; }
        public string? DashboardWarningLights { get; set; }
        public string? Valuables { get; set; }
        public string? DefectsAndImages { get; set; }
        public string? CustomerSignatureUrl { get; set; }
        public long? InspectorId { get; set; }
        public long? CreatedBy { get; set; }
    }
}
