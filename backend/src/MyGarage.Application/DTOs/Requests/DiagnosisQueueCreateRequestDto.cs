namespace MyGarage.DTOs.Requests
{
    public class DiagnosisQueueCreateRequestDto
    {
        public string? DispatchTag { get; set; }
        public bool AutoAssign { get; set; } = false;
        public bool EnforceCapacityGuard { get; set; } = false;
        public bool BypassConflictGuard { get; set; } = false;
        public int? BufferMinutes { get; set; }
        public string? IntakePackageCode { get; set; }
        public bool? CustomerConsentAccepted { get; set; }
        public string? CustomerConsentChannel { get; set; }
        public string? CustomerConsentNote { get; set; }
        public string? AssignmentNote { get; set; }
    }
}
