using System;
namespace MyGarage.DTOs.Requests
{
    public class ConversationCreateDto
    {
        public long? StaffId { get; set; }
        public long CustomerId { get; set; }
        public string? LastMessage { get; set; }
        public long? LastSenderId { get; set; }
        public string? Status { get; set; }
    }
}
