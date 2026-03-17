using System;
namespace MyGarage.DTOs.Requests
{
    public class RevenueSnapshotUpdateDto
    {
        public long BranchId { get; set; }
        public DateOnly SnapshotDate { get; set; }
        public decimal? TotalRevenue { get; set; }
        public decimal? TotalCollected { get; set; }
        public decimal? TotalDebt { get; set; }
        public decimal? TotalExpense { get; set; }
        public decimal? NetProfit { get; set; }
        public decimal? NetCashFlow { get; set; }
    }
}
