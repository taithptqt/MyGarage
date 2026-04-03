using System;
namespace MyGarage.DTOs.Responses
{
    public class ConversationResponseDto
    {
        public long Id { get; set; }
        public long? StaffId { get; set; }
        public long CustomerId { get; set; }
        public string? LastMessage { get; set; }
        public long? LastSenderId { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Status { get; set; }
    }
}
