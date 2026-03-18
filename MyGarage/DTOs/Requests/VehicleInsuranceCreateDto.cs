using System;
namespace MyGarage.DTOs.Requests
{
    public class VehicleInsuranceCreateDto
    {
        public long VehicleId { get; set; }
        public long InsuranceCompanyId { get; set; }
        public string? PolicyNumber { get; set; }
        public DateOnly ExpiryDate { get; set; }
    }
}
