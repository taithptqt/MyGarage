namespace MyGarage.DTOs.Requests
{
    public class HandoverRequestDto
    {
        public long StaffId { get; set; }
        public string ReceiverName { get; set; } = null!;
        public string? ReceiverPhone { get; set; }
        public string? Notes { get; set; }
        public DateTime? HandoverTime { get; set; }
    }
}
