using System;
namespace MyGarage.DTOs.Requests
{
    public class AiForecastUpdateDto
    {
        public long VehicleId { get; set; }
        public string? Type { get; set; }
        public DateOnly PredictedDate { get; set; }
        public decimal? ConfidenceScore { get; set; }
    }
}
