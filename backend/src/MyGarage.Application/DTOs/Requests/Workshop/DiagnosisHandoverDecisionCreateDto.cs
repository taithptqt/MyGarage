namespace MyGarage.DTOs.Requests
{
    public class DiagnosisHandoverDecisionCreateDto
    {
        public bool Approved { get; set; }
        public string? DecisionChannel { get; set; }
        public string? DecisionNote { get; set; }
        public long? DecidedByStaffId { get; set; }
    }
}
