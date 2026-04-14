namespace MyGarage.DTOs.Requests
{
    public class DiagnosisCancelRequestDto
    {
        public long RequestedByStaffId { get; set; }
        public string? Reason { get; set; }
    }
}
