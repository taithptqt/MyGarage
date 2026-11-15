using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class GatePassCommandService : IGatePassCommandService
    {
        private readonly IGatePassService _gatePassService;
        private readonly IServiceOrderService _serviceOrderService;

        public GatePassCommandService(
            IGatePassService gatePassService,
            IServiceOrderService serviceOrderService)
        {
            _gatePassService = gatePassService ?? throw new ArgumentNullException(nameof(gatePassService));
            _serviceOrderService = serviceOrderService ?? throw new ArgumentNullException(nameof(serviceOrderService));
        }

        public Task<GatePassResponseDto?> GetByIdAsync(long id, CancellationToken ct = default)
        {
            return _gatePassService.GetDtoByIdAsync(id);
        }

        public async Task<GatePassCommandResult<GatePassResponseDto>> CreateGatePassAsync(
            GatePassCreateDto? itemDto,
            CancellationToken ct = default)
        {
            try
            {
                if (itemDto == null || itemDto.ServiceOrderId <= 0)
                {
                    return GatePassCommandResult<GatePassResponseDto>.BadRequest(
                        "Thiếu ServiceOrderId hợp lệ để tạo phiếu xuất cổng.");
                }

                var lockError = await ValidateGatePassMutationAsync(
                    itemDto.ServiceOrderId,
                    existingStatus: null,
                    passedAt: null);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return GatePassCommandResult<GatePassResponseDto>.BadRequest(lockError);
                }

                var responseDto = await _gatePassService.CreateDtoAsync(itemDto);
                return GatePassCommandResult<GatePassResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return GatePassCommandResult<GatePassResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<GatePassCommandResult<GatePassResponseDto>> UpdateGatePassAsync(
            long id,
            GatePassUpdateDto? itemDto,
            CancellationToken ct = default)
        {
            try
            {
                var existingItem = await _gatePassService.GetDtoByIdAsync(id);
                if (existingItem == null)
                {
                    return GatePassCommandResult<GatePassResponseDto>.NotFound();
                }

                var targetServiceOrderId = itemDto?.ServiceOrderId > 0
                    ? itemDto.ServiceOrderId
                    : existingItem.ServiceOrderId;
                var lockError = await ValidateGatePassMutationAsync(
                    targetServiceOrderId,
                    existingItem.Status,
                    existingItem.PassedAt);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return GatePassCommandResult<GatePassResponseDto>.BadRequest(lockError);
                }

                if (itemDto == null)
                {
                    return GatePassCommandResult<GatePassResponseDto>.BadRequest(
                        "Dữ liệu cập nhật phiếu xuất cổng không hợp lệ.");
                }

                var responseDto = await _gatePassService.UpdateDtoAsync(id, itemDto);
                if (responseDto == null)
                {
                    return GatePassCommandResult<GatePassResponseDto>.NotFound();
                }

                return GatePassCommandResult<GatePassResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return GatePassCommandResult<GatePassResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<GatePassCommandResult> DeleteGatePassAsync(long id, CancellationToken ct = default)
        {
            try
            {
                var existingItem = await _gatePassService.GetDtoByIdAsync(id);
                if (existingItem == null)
                {
                    return GatePassCommandResult.NotFound();
                }

                var lockError = await ValidateGatePassMutationAsync(
                    existingItem.ServiceOrderId,
                    existingItem.Status,
                    existingItem.PassedAt);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return GatePassCommandResult.BadRequest(lockError);
                }

                var deleted = await _gatePassService.DeleteAsync(id);
                if (!deleted)
                {
                    return GatePassCommandResult.NotFound();
                }

                return GatePassCommandResult.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return GatePassCommandResult.BadRequest(ex.Message);
            }
        }

        private async Task<string?> ValidateGatePassMutationAsync(
            long serviceOrderId,
            string? existingStatus,
            DateTime? passedAt)
        {
            var preCheck = SharedOperationsDomainRules.ValidateGatePassMutation(
                existingStatus,
                passedAt,
                isServiceOrderClosedLocked: false);
            if (!preCheck.IsValid)
            {
                return preCheck.ErrorMessage;
            }

            if (serviceOrderId <= 0)
            {
                return "Thiếu ServiceOrderId hợp lệ để xử lý phiếu xuất cổng.";
            }

            var capability = await _serviceOrderService.GetEditCapabilityAsync(serviceOrderId);
            if (capability == null)
            {
                return "Không tìm thấy service order để xử lý phiếu xuất cổng.";
            }

            var postCheck = SharedOperationsDomainRules.ValidateGatePassMutation(
                existingStatus,
                passedAt,
                capability.IsClosedLocked);
            return postCheck.IsValid ? null : postCheck.ErrorMessage;
        }
    }
}
