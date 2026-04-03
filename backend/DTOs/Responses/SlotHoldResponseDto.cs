using System;

namespace MyGarage.DTOs.Responses
{
    public class SlotHoldResponseDto
    {
        public string HoldToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int HoldMinutes { get; set; }
    }
}

