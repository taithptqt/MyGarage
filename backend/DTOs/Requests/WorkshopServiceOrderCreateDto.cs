using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Requests
{
    public class WorkshopServiceOrderCreateDto
    {
        public string? Code { get; set; }
        public long? CustomerId { get; set; }
        public long? VehicleId { get; set; }
        public long? OriginBranchId { get; set; }
        public long? ExecuteBranchId { get; set; }
        public string? OrderCategory { get; set; }
        public long? ServiceAdvisorId { get; set; }
        public string? CustomerNotes { get; set; }
        public decimal? RequiredDepositAmount { get; set; }
        public string? OldPartsReturnOption { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? EstimatedHandover { get; set; }
        public DateTime? PromisedHandoverTime { get; set; }
        public string? Status { get; set; }

        public bool IsQuickOrder { get; set; }
        public bool IsNewVehicle { get; set; }

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

        public List<long> ServiceIds { get; set; } = new();
    }
}
