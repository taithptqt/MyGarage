using System;

namespace MyGarage.DTOs.Responses.Chat
{
    public class ChatOrderCardDto
    {
        public string OrderType { get; set; } = string.Empty;
        public long OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public decimal? TotalAmount { get; set; }
        public int? ItemCount { get; set; }
        public string? ImageUrl { get; set; }
        public string? BranchName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? DetailUrl { get; set; }
    }
}
