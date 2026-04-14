using System;
namespace MyGarage.DTOs.Responses
{
    public class AiForecastResponseDto
    {
        public long Id { get; set; }
        public long VehicleId { get; set; }
        public string? Type { get; set; }
        public DateOnly PredictedDate { get; set; }
        public decimal? ConfidenceScore { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
