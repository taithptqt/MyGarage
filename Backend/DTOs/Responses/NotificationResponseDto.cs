using System;
namespace MyGarage.DTOs.Responses
{
    public class NotificationResponseDto
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public bool? IsRead { get; set; }
        public DateTime? SentAt { get; set; }
        public string? LinkAction { get; set; }
    }
}
