using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVoucherCampaignService : IGenericService<VoucherCampaign>
    {
        Task<IEnumerable<VoucherCampaign>> GetAllAsync(string? keyword = null, string? status = null, bool includeDeleted = false);
        Task<VoucherCampaign?> GetDetailedByIdAsync(long id, bool includeDeleted = false);
        Task<VoucherCampaign?> AssignVouchersAsync(long campaignId, IEnumerable<long> voucherIds, bool replaceExisting, long? updatedBy = null);
        Task<int> SyncLifecycleStatusesAsync(DateTime? now = null);
        VoucherCampaignResponseDto MapToDto(VoucherCampaign campaign, decimal budgetWarningThresholdPercent = 80m);
        List<VoucherCampaignResponseDto> MapToDtos(IEnumerable<VoucherCampaign> campaigns, decimal budgetWarningThresholdPercent = 80m);
        Task<VoucherCampaignResponseDto?> GetDetailedDtoByIdAsync(long id, bool includeDeleted = false);
        Task<VoucherCampaignResponseDto> CreateDtoAsync(VoucherCampaignCreateDto dto);
        Task<VoucherCampaignResponseDto?> UpdateDtoAsync(long id, VoucherCampaignUpdateDto dto);
        Task<VoucherCampaignResponseDto?> AssignVouchersDtoAsync(long campaignId, IEnumerable<long> voucherIds, bool replaceExisting, long? updatedBy = null);
    }
}
