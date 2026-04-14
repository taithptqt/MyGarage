using System;
namespace MyGarage.DTOs.Requests
{
    public class QcInspectionUpdateDto
    {
        public long ServiceOrderId { get; set; }
        public long QcStaffId { get; set; }
        public long? ServiceOrderDetailId { get; set; }
        public string? Status { get; set; }
        public string? FailReason { get; set; }
        public long? ReworkTechnicianId { get; set; }
    }
}
