namespace MyGarage.DTOs.Requests
{
    public class ServiceOrderGroupSessionCreateDto
    {
        public long CustomerId { get; set; }
        public long BranchId { get; set; }
        public string? CustomerNote { get; set; }
    }
}
