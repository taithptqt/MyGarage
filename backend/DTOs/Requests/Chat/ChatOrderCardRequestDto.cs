using System;

namespace MyGarage.DTOs.Requests.Chat
{
    public class ChatOrderCardRequestDto
    {
        public string? OrderType { get; set; }
        public long OrderId { get; set; }
        public string? OrderCode { get; set; }
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public string? Status { get; set; }
        public string? StatusLabel { get; set; }
        public decimal? TotalAmount { get; set; }
        public int? ItemCount { get; set; }
        public string? ImageUrl { get; set; }
        public string? BranchName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? DetailUrl { get; set; }
    }
}
