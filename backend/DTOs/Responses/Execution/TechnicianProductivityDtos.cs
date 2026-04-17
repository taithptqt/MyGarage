using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses.Execution
{
    public sealed class TechnicianProductivityLineDto
    {
        public long AssignmentId { get; set; }

        public long ServiceOrderDetailId { get; set; }

        public long? ServiceOrderId { get; set; }

        public string? ServiceOrderCode { get; set; }

        public string ServiceName { get; set; } = string.Empty;

        public DateTime WorkStartTime { get; set; }

        public DateTime WorkEndTime { get; set; }

        public int ActualMinutes { get; set; }

        public int FlatRateMinutes { get; set; }

        public decimal EfficiencyPercent { get; set; }

        public decimal SplitRatio { get; set; }

        public decimal TaskRevenue { get; set; }

        public decimal AttributedRevenue { get; set; }
    }

    public sealed class TechnicianProductivitySummaryDto
    {
        public long TechnicianId { get; set; }

        public string TechnicianName { get; set; } = string.Empty;

        public string? Specialty { get; set; }

        public DateOnly FromDate { get; set; }

        public DateOnly ToDate { get; set; }

        public int AssignmentCount { get; set; }

        public int TotalActualMinutes { get; set; }

        public int TotalFlatRateMinutes { get; set; }

        public decimal AverageEfficiencyPercent { get; set; }

        public decimal WeightedEfficiencyPercent { get; set; }

        public decimal TotalTaskRevenue { get; set; }

        public decimal TotalAttributedRevenue { get; set; }

        public int HighEfficiencyCount { get; set; }

        public int LowEfficiencyCount { get; set; }

        public List<TechnicianProductivityLineDto> Items { get; set; } = new();
    }

    public sealed class TechnicianProductivityReportItemDto
    {
        public long TechnicianId { get; set; }

        public string TechnicianName { get; set; } = string.Empty;

        public string? Specialty { get; set; }

        public int AssignmentCount { get; set; }

        public decimal AverageEfficiencyPercent { get; set; }

        public decimal WeightedEfficiencyPercent { get; set; }

        public decimal TotalTaskRevenue { get; set; }

        public decimal TotalAttributedRevenue { get; set; }
    }

    public sealed class TechnicianProductivityReportDto
    {
        public DateOnly FromDate { get; set; }

        public DateOnly ToDate { get; set; }

        public string Period { get; set; } = string.Empty;

        public long? BranchId { get; set; }

        public int TechnicianCount { get; set; }

        public int TotalAssignmentCount { get; set; }

        public decimal AverageEfficiencyPercent { get; set; }

        public decimal TotalTaskRevenue { get; set; }

        public decimal TotalAttributedRevenue { get; set; }

        public List<TechnicianProductivityReportItemDto> Items { get; set; } = new();
    }
}
