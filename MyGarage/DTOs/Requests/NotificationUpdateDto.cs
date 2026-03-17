using System;
namespace MyGarage.DTOs.Requests
{
    public class NotificationUpdateDto
    {
        public long CustomerId { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public bool? IsRead { get; set; }
        public DateTime? SentAt { get; set; }
        public string? LinkAction { get; set; }
    }
}
