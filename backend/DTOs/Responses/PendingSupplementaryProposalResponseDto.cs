using System;

namespace MyGarage.DTOs.Responses
{
    public class PendingSupplementaryProposalResponseDto
    {
        public long ServiceOrderId { get; set; }
        public int PendingCount { get; set; }
        public DateTime? LastRecommendationAt { get; set; }
    }
}
