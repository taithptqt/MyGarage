using System;
namespace MyGarage.DTOs.Requests
{
    public class ExpenseLogUpdateDto
    {
        public long BranchId { get; set; }
        public long? ShiftClosingId { get; set; }
        public string? ExpenseType { get; set; }
        public decimal? Amount { get; set; }
        public string? Description { get; set; }
        public long? StaffId { get; set; }
        public string? ProofUrl { get; set; }
        public string? Status { get; set; }
        public long? ApprovedBy { get; set; }
    }
}
