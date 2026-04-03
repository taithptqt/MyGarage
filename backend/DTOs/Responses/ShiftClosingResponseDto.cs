using System;
namespace MyGarage.DTOs.Responses
{
    public class ShiftClosingResponseDto
    {
        public long Id { get; set; }
        public long BranchId { get; set; }
        public long WorkShiftId { get; set; }
        public long CashierId { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public decimal? OpeningBalance { get; set; }
        public decimal? TotalCashIn { get; set; }
        public decimal? TotalCashOut { get; set; }
        public decimal? ExpectedBalance { get; set; }
        public decimal? ActualBalance { get; set; }
        public decimal? Discrepancy { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
        public long? ManagerId { get; set; }
    }
}
