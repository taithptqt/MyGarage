using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Requests
{
    public class ReceptionAppointmentSaveRequestDto
    {
        public long BranchId { get; set; }
        public bool IsQuickOrder { get; set; }
        public bool IsNewVehicle { get; set; }
        public long? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? GuestName { get; set; }
        public string? GuestPhone { get; set; }
        public string? GuestVehiclePlate { get; set; }
        public long? GuestVehicleModelId { get; set; }
        public string? GuestVehicleColor { get; set; }
        public string? GuestVehicleVin { get; set; }
        public int? GuestVehicleYear { get; set; }
        public string? VehicleLicensePlate { get; set; }
        public long? VehicleModelId { get; set; }
        public string? VehicleColor { get; set; }
        public string? VehicleVin { get; set; }
        public int? VehicleYear { get; set; }
        public string? CustomerNotes { get; set; }
        public long? ReplaceAppointmentId { get; set; }
        public List<ReceptionAppointmentVehiclePlanItemDto> VehiclePlans { get; set; } = new();
    }

    public class ReceptionAppointmentVehiclePlanItemDto
    {
        public string? VehicleKey { get; set; }
        public long? VehicleId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? TotalMinutes { get; set; }
        public string? RequestedServiceIds { get; set; }
        public string? CustomerNotes { get; set; }
        public string? SlotHoldToken { get; set; }
        public string? LicensePlate { get; set; }
        public long? VehicleModelId { get; set; }
        public string? Vin { get; set; }
        public string? Color { get; set; }
    }
}
