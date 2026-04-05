using System;

namespace MyGarage.DTOs.Responses.Chat
{
    public class ConversationListItemDto
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public string? CustomerFullName { get; set; }
        public long? StaffId { get; set; }
        public long? AssignedStaffId { get; set; }
        public long? BranchId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? LastMessage { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
    }
}
