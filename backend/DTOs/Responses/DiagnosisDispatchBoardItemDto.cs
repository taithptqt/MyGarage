using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class DiagnosisDispatchBoardItemDto
    {
        public long ServiceOrderId { get; set; }
        public string? ServiceOrderCode { get; set; }
        public long? ExecuteBranchId { get; set; }
        public string? ServiceOrderStatus { get; set; }
        public IReadOnlyList<long> DiagnosisIds { get; set; } = Array.Empty<long>();
        public string? QueueState { get; set; }
        public string? QueueColor { get; set; }
        public string? DispatchTag { get; set; }
        public long? TeamId { get; set; }
        public string? TeamName { get; set; }
        public long? TechnicianId { get; set; }
        public string? TechnicianName { get; set; }
        public DateTime? RequestedAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public int PendingMinutes { get; set; }
        public int AssignedMinutes { get; set; }
        public bool IsSupportRequested { get; set; }
        public bool IsCancelPendingApproval { get; set; }
        public string? CancelReason { get; set; }
    }
}
