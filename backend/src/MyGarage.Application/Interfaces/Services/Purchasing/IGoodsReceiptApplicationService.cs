using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Purchasing.DTOs;

namespace MyGarage.Application.Purchasing;

public interface IGoodsReceiptApplicationService
{
    Task<GoodsReceiptPagedResultDto> GetGoodsReceiptsAsync(
        GoodsReceiptFilterDto filter,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<GoodsReceiptDetailDto?> GetGoodsReceiptByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<PurchaseOrderReceiveResultDto> CreateGoodsReceiptAsync(
        GoodsReceiptCreateInput input,
        UserActorContext actor,
        CancellationToken ct = default);
}
