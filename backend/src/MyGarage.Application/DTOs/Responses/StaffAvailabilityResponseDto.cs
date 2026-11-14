namespace MyGarage.DTOs.Responses
{
    public sealed class StaffAvailabilityResponseDto
    {
        public long StaffId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public long? BranchId { get; set; }
        public long? TeamId { get; set; }
        public string? Specialty { get; set; }
        public bool IsBusyInDate { get; set; }
        public int AssignedTaskCountInDate { get; set; }
    }
}
