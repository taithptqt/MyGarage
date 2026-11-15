using System.Text.Json;
using System.Threading.Tasks;

namespace MyGarage.Application.Accounting;

public interface IAccountingTableAndInvoiceService
{
    AccountingAccessResult AuthorizeTableAccess(string tableKey, AccountingActorContext actor);
    AccountingAccessResult AuthorizeAccountingAccess(AccountingActorContext actor);
    AccountingAccessResult AuthorizeFinanceApprovalAccess(AccountingActorContext actor);

    Task<AccountingCommandResult> GetTableAsync(string tableKey, int limit, long? branchId);
    Task<AccountingCommandResult> GetTableRowByIdAsync(string tableKey, long id, long? branchId);
    Task<AccountingCommandResult> CreateTableRowAsync(string tableKey, JsonElement payload, long? branchId);
    Task<AccountingCommandResult> UpdateTableRowAsync(string tableKey, long id, JsonElement payload, long? branchId);
    Task<AccountingCommandResult> DeleteTableRowAsync(string tableKey, long id, long? branchId);

    Task<AccountingCommandResult> CreateSupplierInvoiceAsync(JsonElement payload, long? branchId);
    Task<AccountingCommandResult> UpdateSupplierInvoiceAsync(long id, JsonElement payload, long? branchId);
    Task<AccountingCommandResult> CreateSupplierInvoiceDetailAsync(JsonElement payload, long? branchId);
    Task<AccountingCommandResult> UpdateSupplierInvoiceDetailAsync(long id, JsonElement payload, long? branchId);
}
