using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IGrnSnapshotService
    {
        Task<object> RebuildDailySnapshotAsync(DateOnly snapshotDate, long? branchId = null);
        Task<IReadOnlyList<object>> GetTrendAsync(DateOnly fromDate, DateOnly toDate, long? branchId = null);
    }
}
