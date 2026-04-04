using System;
namespace MyGarage.DTOs.Responses
{
    public class VehicleInsuranceResponseDto
    {
        public long Id { get; set; }
        public long VehicleId { get; set; }
        public long InsuranceCompanyId { get; set; }
        public string? PolicyNumber { get; set; }
        public DateOnly ExpiryDate { get; set; }
    }
}
