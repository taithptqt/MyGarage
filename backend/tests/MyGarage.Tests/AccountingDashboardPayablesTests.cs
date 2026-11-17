using System.Text.Json;
using MyGarage.Application.Accounting;
using MyGarage.Application.Common;
using MyGarage.Interfaces;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class AccountingDashboardPayablesTests
{
    [Fact]
    public async Task Dashboard_UsesScopedPayablesForSummaryAndCsv()
    {
        var crud = new FakeCrud();
        var service = new AccountingFinanceWorkflowService(crud);
        var result = await service.GetDashboardV2Async(null, null, 20, 7);
        var payload = JsonSerializer.SerializeToElement(result.Payload);
        Assert.True(payload.GetProperty("payablesAvailable").GetBoolean());
        Assert.Equal(500m, payload.GetProperty("summary").GetProperty("totalOutstandingAp").GetDecimal());
        Assert.Equal(200m, payload.GetProperty("summary").GetProperty("overdueApAmount").GetDecimal());
        Assert.Single(payload.GetProperty("apWatchlist").EnumerateArray());
        Assert.Equal(7L, crud.BranchId);
        Assert.Equal(20, crud.Limit);
        var csv = System.Text.Encoding.UTF8.GetString((await service.ExportDashboardV2CsvAsync(null, null, 20, 7)).Content);
        Assert.Contains("summary,totalOutstandingAp,500", csv);
        Assert.Contains("summary,overdueApAmount,200", csv);
    }

    private sealed class FakeCrud : IAccountingCrudService
    {
        public long? BranchId { get; private set; }
        public int? Limit { get; private set; }
        public bool TryGetConfig(string key, out AccountingCrudTableConfig config) { config = new("dbo.accounts_payable", new HashSet<string>()); return true; }
        public Task<bool> TableExistsAsync(AccountingCrudTableConfig config) => Task.FromResult(true);
        public Task<bool> ViewExistsAsync(string view) => Task.FromResult(false);
        public Task<IReadOnlyList<Dictionary<string, object?>>> GetAllAsync(string tableKey, int limit = 500, long? branchId = null) => throw new NotSupportedException();
        public Task<Dictionary<string, object?>?> GetByIdAsync(string tableKey, long id, long? branchId = null) => throw new NotSupportedException();
        public Task<Dictionary<string, object?>?> InsertAsync(string tableKey, JsonElement payload, long? branchId = null) => throw new NotSupportedException();
        public Task<Dictionary<string, object?>?> UpdateAsync(string tableKey, long id, JsonElement payload, long? branchId = null) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string tableKey, long id, long? branchId = null) => throw new NotSupportedException();
        public Task<int> ExecuteAsync(string sql, IEnumerable<QueryParam>? parameters = null) => throw new NotSupportedException();
        public Task<T?> ScalarAsync<T>(string sql, IEnumerable<QueryParam>? parameters = null) => Task.FromResult((T?)(object)1);
        public Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(string sql, IEnumerable<QueryParam>? parameters = null)
        {
            if (!sql.Contains("FROM dbo.accounts_payable")) return Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>(Array.Empty<Dictionary<string, object?>>());
            var args = parameters!.ToList();
            BranchId = Convert.ToInt64(args.Single(p => p.Name == "@branchId").Value);
            Limit = Convert.ToInt32(args.Single(p => p.Name == "@limit").Value);
            var row = sql.Contains("SUM(outstanding_amount)")
                ? new Dictionary<string, object?> { ["totalOutstanding"] = 500m, ["overdueAmount"] = 200m }
                : new Dictionary<string, object?> { ["id"] = 1L, ["outstandingAmount"] = 200m };
            return Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>(new[] { row });
        }
    }
}