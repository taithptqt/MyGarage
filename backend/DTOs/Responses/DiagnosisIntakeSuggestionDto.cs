using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class DiagnosisIntakeSuggestionDto
    {
        public long ServiceOrderId { get; set; }
        public string? DispatchTag { get; set; }
        public DateTime CheckInTime { get; set; }
        public int BufferMinutes { get; set; }
        public int MainServiceMinutes { get; set; }
        public DateTime BaseFinishAt { get; set; }
        public DateTime PredictedFinishAt { get; set; }
        public DateTime T1EstimatedHandover { get; set; }
        public string ScoredPackageCode { get; set; } = "MINI_10";
        public string ScoredPackageName { get; set; } = "Mini Check (10p)";
        public string FinalPackageCode { get; set; } = "MINI_10";
        public string FinalPackageName { get; set; } = "Mini Check (10p)";
        public int PackageNominalMinutes { get; set; }
        public int PackageEffectiveMinutes { get; set; }
        public bool RequiresCustomerConsent { get; set; }
        public bool RequiresBayBlock { get; set; }
        public bool UsedConflictFallback { get; set; }
        public string ConflictLevel { get; set; } = "GREEN";
        public int ConflictMinutes { get; set; }
        public bool CanApply { get; set; }
        public long? NextBookingId { get; set; }
        public DateTime? NextBookingStartTime { get; set; }
        public long? SuggestedTeamId { get; set; }
        public string? SuggestedTeamName { get; set; }
        public long? SuggestedLeadTechnicianId { get; set; }
        public string? SuggestedLeadTechnicianName { get; set; }
        public long? SuggestedAssistantTechnicianId { get; set; }
        public string? SuggestedAssistantTechnicianName { get; set; }
        public long? SuggestedBayId { get; set; }
        public string? SuggestedBayCode { get; set; }
        public IReadOnlyList<string> Reasons { get; set; } = new List<string>();
    }
}
