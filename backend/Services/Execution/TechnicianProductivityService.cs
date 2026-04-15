using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public sealed class TechnicianProductivityService : ITechnicianProductivityService
    {
        private readonly IGarageContext _context;

        public TechnicianProductivityService(IGarageContext context)
        {
            _context = context;
        }

        public async Task<TechnicianProductivitySummaryDto?> GetTechnicianProductivityAsync(
            long technicianId,
            DateOnly? fromDate = null,
            DateOnly? toDate = null)
        {
            if (technicianId <= 0)
            {
                return null;
            }

            var (from, to) = ResolveDateRange(null, fromDate, toDate);
            var fromDateTime = from.ToDateTime(TimeOnly.MinValue);
            var toExclusive = to.ToDateTime(TimeOnly.MinValue).AddDays(1);

            var technician = await _context.Staffs
                .AsNoTracking()
                .Where(x => x.Id == technicianId && x.IsDeleted != true)
                .Select(x => new { x.Id, x.FullName, x.Specialty })
                .FirstOrDefaultAsync();
            if (technician == null)
            {
                return null;
            }

            var rows = await QueryBaseRowsAsync(fromDateTime, toExclusive, technicianId, null);
            var orderedRows = rows
                .OrderByDescending(x => x.WorkEndTime)
                .ThenByDescending(x => x.AssignmentId)
                .ToList();

            var summary = BuildSummary(
                technician.Id,
                technician.FullName,
                technician.Specialty,
                from,
                to,
                orderedRows);
            return summary;
        }

        public async Task<TechnicianProductivityReportDto> GetProductivityReportAsync(
            string? period = null,
            DateOnly? fromDate = null,
            DateOnly? toDate = null,
            long? branchId = null,
            int top = 30)
        {
            var normalizedTop = Math.Clamp(top, 1, 200);
            var (from, to) = ResolveDateRange(period, fromDate, toDate);
            var fromDateTime = from.ToDateTime(TimeOnly.MinValue);
            var toExclusive = to.ToDateTime(TimeOnly.MinValue).AddDays(1);

            var rows = await QueryBaseRowsAsync(fromDateTime, toExclusive, null, branchId);
            var grouped = rows
                .GroupBy(x => new { x.TechnicianId, x.TechnicianName, x.Specialty })
                .Select(group =>
                {
                    var assignmentCount = group.Count();
                    var totalActual = group.Sum(x => x.ActualMinutes);
                    var totalFlat = group.Sum(x => x.FlatRateMinutes);
                    var avgEfficiency = assignmentCount == 0
                        ? 0m
                        : Math.Round(group.Average(x => x.EfficiencyPercent), 2, MidpointRounding.AwayFromZero);
                    var weightedEfficiency = totalActual <= 0
                        ? 0m
                        : Math.Round((totalFlat * 100m) / totalActual, 2, MidpointRounding.AwayFromZero);

                    return new TechnicianProductivityReportItemDto
                    {
                        TechnicianId = group.Key.TechnicianId,
                        TechnicianName = group.Key.TechnicianName,
                        Specialty = group.Key.Specialty,
                        AssignmentCount = assignmentCount,
                        AverageEfficiencyPercent = avgEfficiency,
                        WeightedEfficiencyPercent = weightedEfficiency,
                        TotalTaskRevenue = Math.Round(group.Sum(x => x.TaskRevenue), 2, MidpointRounding.AwayFromZero),
                        TotalAttributedRevenue = Math.Round(group.Sum(x => x.AttributedRevenue), 2, MidpointRounding.AwayFromZero)
                    };
                })
                .OrderByDescending(x => x.WeightedEfficiencyPercent)
                .ThenByDescending(x => x.AssignmentCount)
                .Take(normalizedTop)
                .ToList();

            var totalAssignmentCount = grouped.Sum(x => x.AssignmentCount);
            var averageEfficiency = grouped.Count == 0
                ? 0m
                : Math.Round(grouped.Average(x => x.WeightedEfficiencyPercent), 2, MidpointRounding.AwayFromZero);

            return new TechnicianProductivityReportDto
            {
                FromDate = from,
                ToDate = to,
                Period = NormalizePeriod(period, fromDate, toDate),
                BranchId = branchId > 0 ? branchId : null,
                TechnicianCount = grouped.Count,
                TotalAssignmentCount = totalAssignmentCount,
                AverageEfficiencyPercent = averageEfficiency,
                TotalTaskRevenue = Math.Round(grouped.Sum(x => x.TotalTaskRevenue), 2, MidpointRounding.AwayFromZero),
                TotalAttributedRevenue = Math.Round(grouped.Sum(x => x.TotalAttributedRevenue), 2, MidpointRounding.AwayFromZero),
                Items = grouped
            };
        }

        private async Task<List<ProductivityBaseRow>> QueryBaseRowsAsync(
            DateTime fromDateTime,
            DateTime toExclusive,
            long? technicianId,
            long? branchId)
        {
            var query = _context.ServiceStaffAssignments
                .AsNoTracking()
                .Where(x =>
                    x.ActualStartTime.HasValue &&
                    x.ActualEndTime.HasValue &&
                    x.ActualEndTime.Value >= fromDateTime &&
                    x.ActualEndTime.Value < toExclusive);

            if (technicianId.HasValue && technicianId.Value > 0)
            {
                query = query.Where(x => x.TechnicianId == technicianId.Value);
            }

            if (branchId.HasValue && branchId.Value > 0)
            {
                query = query.Where(x => x.ServiceOrderDetail.ServiceOrder.ExecuteBranchId == branchId.Value);
            }

            var rows = await query
                .Select(x => new
                {
                    AssignmentId = x.Id,
                    TechnicianId = x.TechnicianId,
                    TechnicianName = x.Technician.FullName,
                    Specialty = x.Technician.Specialty,
                    ServiceOrderDetailId = x.ServiceOrderDetailId,
                    ServiceOrderId = (long?)x.ServiceOrderDetail.ServiceOrderId,
                    ServiceOrderCode = x.ServiceOrderDetail.ServiceOrder.Code,
                    ServiceName = x.ServiceOrderDetail.StandardService != null
                        ? x.ServiceOrderDetail.StandardService.Name
                        : x.ServiceOrderDetail.ItemName,
                    WorkStartTime = x.ActualStartTime!.Value,
                    WorkEndTime = x.ActualEndTime!.Value,
                    ActualMinutes = EF.Functions.DateDiffMinute(x.ActualStartTime!.Value, x.ActualEndTime!.Value),
                    FlatRateMinutes = x.ServiceOrderDetail.EstimatedMinutes
                        ?? (x.ServiceOrderDetail.StandardService != null
                            ? (int?)x.ServiceOrderDetail.StandardService.StandardMinutes
                            : null)
                        ?? 0,
                    SplitRatio = x.SplitRatio,
                    TaskRevenue = x.ServiceOrderDetail.FinalAmount
                })
                .ToListAsync();

            return rows
                .Select(x =>
                {
                    var actualMinutes = Math.Max(1, x.ActualMinutes);
                    var flatRate = Math.Max(0, x.FlatRateMinutes);
                    var efficiency = flatRate <= 0
                        ? 0m
                        : Math.Round((flatRate * 100m) / actualMinutes, 2, MidpointRounding.AwayFromZero);
                    var split = x.SplitRatio <= 0m ? 1m : x.SplitRatio;
                    var taskRevenue = x.TaskRevenue;
                    var attributedRevenue = Math.Round(taskRevenue * split, 2, MidpointRounding.AwayFromZero);

                    return new ProductivityBaseRow
                    {
                        AssignmentId = x.AssignmentId,
                        TechnicianId = x.TechnicianId,
                        TechnicianName = x.TechnicianName ?? string.Empty,
                        Specialty = x.Specialty,
                        ServiceOrderDetailId = x.ServiceOrderDetailId,
                        ServiceOrderId = x.ServiceOrderId,
                        ServiceOrderCode = x.ServiceOrderCode,
                        ServiceName = string.IsNullOrWhiteSpace(x.ServiceName) ? "-" : x.ServiceName,
                        WorkStartTime = x.WorkStartTime,
                        WorkEndTime = x.WorkEndTime,
                        ActualMinutes = actualMinutes,
                        FlatRateMinutes = flatRate,
                        EfficiencyPercent = efficiency,
                        SplitRatio = split,
                        TaskRevenue = taskRevenue,
                        AttributedRevenue = attributedRevenue
                    };
                })
                .ToList();
        }

        private static TechnicianProductivitySummaryDto BuildSummary(
            long technicianId,
            string technicianName,
            string? specialty,
            DateOnly from,
            DateOnly to,
            List<ProductivityBaseRow> rows)
        {
            var assignmentCount = rows.Count;
            var totalActual = rows.Sum(x => x.ActualMinutes);
            var totalFlat = rows.Sum(x => x.FlatRateMinutes);
            var averageEfficiency = assignmentCount == 0
                ? 0m
                : Math.Round(rows.Average(x => x.EfficiencyPercent), 2, MidpointRounding.AwayFromZero);
            var weightedEfficiency = totalActual <= 0
                ? 0m
                : Math.Round((totalFlat * 100m) / totalActual, 2, MidpointRounding.AwayFromZero);

            return new TechnicianProductivitySummaryDto
            {
                TechnicianId = technicianId,
                TechnicianName = technicianName,
                Specialty = specialty,
                FromDate = from,
                ToDate = to,
                AssignmentCount = assignmentCount,
                TotalActualMinutes = totalActual,
                TotalFlatRateMinutes = totalFlat,
                AverageEfficiencyPercent = averageEfficiency,
                WeightedEfficiencyPercent = weightedEfficiency,
                TotalTaskRevenue = Math.Round(rows.Sum(x => x.TaskRevenue), 2, MidpointRounding.AwayFromZero),
                TotalAttributedRevenue = Math.Round(rows.Sum(x => x.AttributedRevenue), 2, MidpointRounding.AwayFromZero),
                HighEfficiencyCount = rows.Count(x => x.EfficiencyPercent > 100m),
                LowEfficiencyCount = rows.Count(x => x.EfficiencyPercent < 80m),
                Items = rows.Select(x => new TechnicianProductivityLineDto
                {
                    AssignmentId = x.AssignmentId,
                    ServiceOrderDetailId = x.ServiceOrderDetailId,
                    ServiceOrderId = x.ServiceOrderId,
                    ServiceOrderCode = x.ServiceOrderCode,
                    ServiceName = x.ServiceName,
                    WorkStartTime = x.WorkStartTime,
                    WorkEndTime = x.WorkEndTime,
                    ActualMinutes = x.ActualMinutes,
                    FlatRateMinutes = x.FlatRateMinutes,
                    EfficiencyPercent = x.EfficiencyPercent,
                    SplitRatio = x.SplitRatio,
                    TaskRevenue = x.TaskRevenue,
                    AttributedRevenue = x.AttributedRevenue
                }).ToList()
            };
        }

        private static (DateOnly FromDate, DateOnly ToDate) ResolveDateRange(
            string? period,
            DateOnly? fromDate,
            DateOnly? toDate)
        {
            var now = VietnamTime.Now.Date;
            if (fromDate.HasValue || toDate.HasValue)
            {
                var from = fromDate ?? DateOnly.FromDateTime(now.AddDays(-30));
                var to = toDate ?? DateOnly.FromDateTime(now);
                if (to < from)
                {
                    (from, to) = (to, from);
                }

                return (from, to);
            }

            var normalized = NormalizePeriod(period, null, null);
            var today = DateOnly.FromDateTime(now);

            return normalized switch
            {
                "today" => (today, today),
                "week" => (today.AddDays(-6), today),
                "month" => (new DateOnly(today.Year, today.Month, 1), today),
                "quarter" => (new DateOnly(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1), today),
                "year" => (new DateOnly(today.Year, 1, 1), today),
                _ => (today.AddDays(-30), today)
            };
        }

        private static string NormalizePeriod(string? period, DateOnly? fromDate, DateOnly? toDate)
        {
            if (fromDate.HasValue || toDate.HasValue)
            {
                return "custom";
            }

            var normalized = (period ?? "month").Trim().ToLowerInvariant();
            return normalized switch
            {
                "today" => "today",
                "week" => "week",
                "month" => "month",
                "quarter" => "quarter",
                "year" => "year",
                _ => "month"
            };
        }

        private sealed class ProductivityBaseRow
        {
            public long AssignmentId { get; set; }

            public long TechnicianId { get; set; }

            public string TechnicianName { get; set; } = string.Empty;

            public string? Specialty { get; set; }

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
    }
}
