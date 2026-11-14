using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Vouchers.DTOs;

namespace MyGarage.Application.Vouchers;

public interface IVoucherApplicationService
{
    Task<VoucherAdminListResultDto> GetAdminListPageAsync(VoucherAdminListFilterInput filter, UserActorContext actor, CancellationToken ct = default);
    Task<VoucherDto?> GetByIdAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<VoucherDto> CreateVoucherAsync(VoucherCreateInput input, UserActorContext actor, CancellationToken ct = default);
    Task<VoucherDto?> UpdateVoucherAsync(long id, VoucherUpdateInput input, UserActorContext actor, CancellationToken ct = default);
    Task<VoucherDto?> PatchStatusAsync(long id, string status, UserActorContext actor, CancellationToken ct = default);
    Task<bool> DeleteVoucherAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<VoucherValidationResultDto> ValidateVoucherAsync(VoucherValidationInput input, UserActorContext actor, CancellationToken ct = default);
}
