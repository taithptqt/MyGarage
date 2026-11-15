using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Inventory;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class GrnSnapshotService : IGrnSnapshotService
    {
        private readonly IInventoryOperationsStore _store;

        public GrnSnapshotService(IInventoryOperationsStore store)
        {
            _store = store;
        }

        public async Task<object> RebuildDailySnapshotAsync(DateOnly snapshotDate, long? branchId = null)
        {
            var start = snapshotDate.ToDateTime(TimeOnly.MinValue);
            var end = start.AddDays(1);

            var grns = await _store.GetGoodsReceiptNotesForSnapshotAsync(start, end, branchId);
            var grnIds = grns.Select(x => x.Id).ToList();
            var items = await _store.GetGoodsReceiptNoteItemsAsync(grnIds);

            var branchGroups = grns.GroupBy(x => x.Warehouse.BranchId).ToList();
            var touchedBranchIds = new HashSet<long>();
            foreach (var group in branchGroups)
            {
                var branchGrnIds = group.Select(x => x.Id).ToHashSet();
                var branchItems = items.Where(x => branchGrnIds.Contains(x.GoodsReceiptNoteId)).ToList();

                var row = await _store.GetGrnDailySnapshotAsync(group.Key, snapshotDate);
                if (row == null)
                {
                    row = new GrnDailySnapshot
                    {
                        BranchId = group.Key,
                        SnapshotDate = snapshotDate,
                        CreatedAt = VietnamTime.Now
                    };
                    _store.AddGrnDailySnapshot(row);
                }

                row.GrnCount = group.Count();
                row.TotalReceivedAmount = branchItems.Sum(x => x.LineAmount);
                row.TotalReceivedQuantity = branchItems.Sum(x => x.ReceivedQuantity);
                row.DistinctProducts = branchItems.Select(x => x.ProductId).Distinct().Count();
                touchedBranchIds.Add(group.Key);
            }

            if (branchId.HasValue && branchId.Value > 0 && !touchedBranchIds.Contains(branchId.Value))
            {
                var existed = await _store.GetGrnDailySnapshotAsync(branchId.Value, snapshotDate);
                if (existed != null)
                {
                    _store.RemoveGrnDailySnapshot(existed);
                }
            }

            await _store.SaveChangesAsync();

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

            var rows = await _store.GetGrnDailySnapshotsRangeAsync(fromDate, toDate, branchId);
            return rows
                .Select(x => (object)new
                {
                    x.BranchId,
                    x.SnapshotDate,
                    x.GrnCount,
                    x.TotalReceivedAmount,
                    x.TotalReceivedQuantity,
                    x.DistinctProducts
                })
                .ToList();
        }
    }
}
