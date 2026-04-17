using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses.Execution
{
    public sealed class WorkshopDispatchBoardDto
    {
        public long BranchId { get; set; }
        public DateTime Date { get; set; }
        public List<WorkshopDispatchCardDto> ServiceOrders { get; set; } = new();
    }

    public sealed class WorkshopDispatchCardDto
    {
        public long ServiceOrderId { get; set; }
        public string ServiceOrderCode { get; set; } = string.Empty;
        public string ServiceOrderStatus { get; set; } = string.Empty;
        public long VehicleId { get; set; }
        public string LicensePlate { get; set; } = "-";
        public string CustomerName { get; set; } = "-";
        public long? BayId { get; set; }
        public string? BayName { get; set; }
        public string? BayStatus { get; set; }
        public DateTime? BayAllocatedAt { get; set; }
        public int TotalLaborLines { get; set; }
        public int TotalPartLines { get; set; }
        public int AssignmentCount { get; set; }
        public int DistinctTechnicianCount { get; set; }
        public int MaterialRequestCount { get; set; }
        public int MaterialRequestPendingLines { get; set; }
        public int MaterialRequestIssuedLines { get; set; }
        public bool ReadyForStorekeeper { get; set; }
        public List<WorkshopDispatchDetailDto> Details { get; set; } = new();
    }

    public sealed class WorkshopDispatchDetailDto
    {
        public long ServiceOrderDetailId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string RequiredSpecialty { get; set; } = "GENERAL";
        public List<WorkshopDispatchAssignmentDto> Assignments { get; set; } = new();
    }

    public sealed class WorkshopDispatchAssignmentDto
    {
        public long AssignmentId { get; set; }
        public long TechnicianId { get; set; }
        public string TechnicianName { get; set; } = string.Empty;
        public string? TechnicianSpecialty { get; set; }
        public string RoleInTask { get; set; } = string.Empty;
        public decimal SplitRatio { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }
    }
}
