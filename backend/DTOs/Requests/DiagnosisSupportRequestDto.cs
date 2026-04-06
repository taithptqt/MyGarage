namespace MyGarage.DTOs.Requests
{
    public class DiagnosisSupportRequestDto
    {
        public long RequestedByStaffId { get; set; }
        public string? Note { get; set; }
    }
}
