namespace MyGarage.DTOs.Requests
{
    public class DiagnosisCancelDecisionDto
    {
        public long ManagerId { get; set; }
        public bool Approve { get; set; }
        public string? Note { get; set; }
    }
}
