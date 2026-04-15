using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class GrnSnapshotService : IGrnSnapshotService
    {
        private readonly IGarageContext _context;

        public GrnSnapshotService(IGarageContext context)
        {
            _context = context;
        }

        public async Task<object> RebuildDailySnapshotAsync(DateOnly snapshotDate, long? branchId = null)
        {
            var start = snapshotDate.ToDateTime(TimeOnly.MinValue);
            var end = start.AddDays(1);

            var grnQuery = _context.GoodsReceiptNotes
                .AsNoTracking()
                .Include(x => x.Warehouse)
                .Where(x => x.ReceivedAt >= start && x.ReceivedAt < end);
            if (branchId.HasValue && branchId.Value > 0)
            {
                grnQuery = grnQuery.Where(x => x.Warehouse.BranchId == branchId.Value);
            }

            var grns = await grnQuery.ToListAsync();
            var grnIds = grns.Select(x => x.Id).ToList();
            var items = await _context.GoodsReceiptNoteItems
                .AsNoTracking()
                .Where(x => grnIds.Contains(x.GoodsReceiptNoteId))
                .ToListAsync();

            var branchGroups = grns.GroupBy(x => x.Warehouse.BranchId).ToList();
            var touchedBranchIds = new HashSet<long>();
            foreach (var group in branchGroups)
            {
                var branchGrnIds = group.Select(x => x.Id).ToHashSet();
                var branchItems = items.Where(x => branchGrnIds.Contains(x.GoodsReceiptNoteId)).ToList();

                var row = await _context.GrnDailySnapshots
                    .FirstOrDefaultAsync(x => x.BranchId == group.Key && x.SnapshotDate == snapshotDate);
                if (row == null)
                {
                    row = new GrnDailySnapshot
                    {
                        BranchId = group.Key,
                        SnapshotDate = snapshotDate,
                        CreatedAt = VietnamTime.Now
                    };
                    _context.GrnDailySnapshots.Add(row);
                }

                row.GrnCount = group.Count();
                row.TotalReceivedAmount = branchItems.Sum(x => x.LineAmount);
                row.TotalReceivedQuantity = branchItems.Sum(x => x.ReceivedQuantity);
                row.DistinctProducts = branchItems.Select(x => x.ProductId).Distinct().Count();
                touchedBranchIds.Add(group.Key);
            }

            if (branchId.HasValue && branchId.Value > 0 && !touchedBranchIds.Contains(branchId.Value))
            {
                var existed = await _context.GrnDailySnapshots
                    .FirstOrDefaultAsync(x => x.BranchId == branchId.Value && x.SnapshotDate == snapshotDate);
                if (existed != null)
                {
                    _context.GrnDailySnapshots.Remove(existed);
                }
            }

            await _context.SaveChangesAsync();

            return new
            {
                snapshotDate,
                branchId,
                branchCount = touchedBranchIds.Count,
                snapshots = touchedBranchIds.Count
            };
        }

        public async Task<IReadOnlyList<object>> GetTrendAsync(DateOnly fromDate, DateOnly toDate, long? branchId = null)
        {
            if (toDate < fromDate)
            {
                return Array.Empty<object>();
            }

            var query = _context.GrnDailySnapshots
                .AsNoTracking()
                .Where(x => x.SnapshotDate >= fromDate && x.SnapshotDate <= toDate);
            if (branchId.HasValue && branchId.Value > 0)
            {
                query = query.Where(x => x.BranchId == branchId.Value);
            }

            var rows = await query
                .OrderBy(x => x.SnapshotDate)
                .ThenBy(x => x.BranchId)
                .Select(x => (object)new
                {
                    x.BranchId,
                    x.SnapshotDate,
                    x.GrnCount,
                    x.TotalReceivedAmount,
                    x.TotalReceivedQuantity,
                    x.DistinctProducts
                })
                .ToListAsync();

            return rows;
        }
    }
}
