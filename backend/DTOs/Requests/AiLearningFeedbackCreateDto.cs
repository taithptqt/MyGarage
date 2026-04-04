using System;
namespace MyGarage.DTOs.Requests
{
    public class AiLearningFeedbackCreateDto
    {
        public string? AiFeature { get; set; }
        public string? InputData { get; set; }
        public string? AiOutput { get; set; }
        public string? UserCorrectedOutput { get; set; }
        public bool? IsUseful { get; set; }
    }
}
