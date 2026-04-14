using System;
namespace MyGarage.DTOs.Responses
{
    public class ConversationResponseDto
    {
        public long Id { get; set; }
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
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Status { get; set; }
    }
}
