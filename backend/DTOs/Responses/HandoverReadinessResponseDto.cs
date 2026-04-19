namespace MyGarage.DTOs.Responses
{
    public class HandoverReadinessResponseDto
    {
        public long ServiceOrderId { get; set; }
        public bool IsPaid { get; set; }
        public string PaymentStatus { get; set; } = null!;
        public bool IsQcPassed { get; set; }
        public string QcStatus { get; set; } = null!;
        public bool AreAllTasksCompleted { get; set; }
        public bool CanHandover => IsPaid && IsQcPassed && AreAllTasksCompleted;
        public string? BlockingReason { get; set; }
    }
}
