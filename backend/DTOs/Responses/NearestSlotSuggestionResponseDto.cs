using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class NearestSlotSuggestionResponseDto
    {
        public long BranchId { get; set; }
        public DateTime RequestedCheckInAt { get; set; }
        public string? RequestedServiceIds { get; set; }
        public int ServiceMinutes { get; set; }
        public int SetupMinutes { get; set; }
        public int BufferMinutes { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public DateTime? SuggestedStartTime { get; set; }
        public DateTime? SuggestedEndTime { get; set; }
        public int SuggestionDeltaMinutes { get; set; }
        public bool IsExactOrEarlier { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<AvailableSlotResponseDto> Alternatives { get; set; } = new();
    }
}