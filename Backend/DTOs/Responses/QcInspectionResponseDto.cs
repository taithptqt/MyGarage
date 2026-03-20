using System;
namespace MyGarage.DTOs.Responses
{
    public class QcInspectionResponseDto
    {
        public long Id { get; set; }
        public long ServiceOrderId { get; set; }
        public long QcStaffId { get; set; }
        public long? ServiceOrderDetailId { get; set; }
        public string? Status { get; set; }
        public string? FailReason { get; set; }
        public long? ReworkTechnicianId { get; set; }
    }
}
