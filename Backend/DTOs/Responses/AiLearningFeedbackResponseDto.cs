using System;
namespace MyGarage.DTOs.Responses
{
    public class AiLearningFeedbackResponseDto
    {
        public long Id { get; set; }
        public string? AiFeature { get; set; }
        public string? InputData { get; set; }
        public string? AiOutput { get; set; }
        public string? UserCorrectedOutput { get; set; }
        public bool? IsUseful { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
