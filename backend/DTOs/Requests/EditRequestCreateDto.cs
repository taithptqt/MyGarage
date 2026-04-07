namespace MyGarage.DTOs.Requests
{
    public class EditRequestCreateDto
    {
        public string? Section { get; set; }
        public string? Reason { get; set; }
        public long? RequestedByUserId { get; set; }
    }

    public class EditRequestDecisionDto
    {
        public string? Decision { get; set; }
        public string? Note { get; set; }
    }
}
