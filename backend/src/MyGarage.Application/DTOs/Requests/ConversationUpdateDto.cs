using System;
namespace MyGarage.DTOs.Requests
{
    public class ConversationUpdateDto
    {
        public long? BranchId { get; set; }
        public long? StaffId { get; set; }
        public long? AssignedStaffId { get; set; }
        public long CustomerId { get; set; }
        public long? AppointmentId { get; set; }
        public long? ServiceOrderId { get; set; }
        public long? VehicleId { get; set; }
        public string? LastMessage { get; set; }
        public long? LastMessageId { get; set; }
        public long? LastSenderId { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public string? Status { get; set; }
    }
}
