using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class VehicleConditionLogCommandFacade : IVehicleConditionLogCommandFacade
    {
        private readonly IVehicleConditionLogService _vehicleConditionLogService;
        private readonly IDiagnosisService _diagnosisService;
        private readonly IVehicleConditionLogQueryService _queryService;
        private readonly IMapper _mapper;
        private readonly ICloudStorageService _cloudStorageService;

        public VehicleConditionLogCommandFacade(
            IVehicleConditionLogService vehicleConditionLogService,
            IDiagnosisService diagnosisService,
            IVehicleConditionLogQueryService queryService,
            IMapper mapper,
            ICloudStorageService cloudStorageService)
        {
            _vehicleConditionLogService = vehicleConditionLogService;
            _diagnosisService = diagnosisService;
            _queryService = queryService;
            _mapper = mapper;
            _cloudStorageService = cloudStorageService;
        }

        public async Task<VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>> CreateRequestAsync(
            long serviceOrderId,
            string? requestType)
        {
            var type = VehicleConditionLogCommandHelper.NormalizeLogType(requestType);
            if (type != VehicleConditionLogCommandHelper.LogTypeCheckIn &&
                type != VehicleConditionLogCommandHelper.LogTypeCheckOut)
            {
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest("requestType must be CHECK_IN or CHECK_OUT.");
            }

            var orderExists = await _vehicleConditionLogService.ServiceOrderExistsAsync(serviceOrderId);
            if (!orderExists)
            {
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.NotFound("Không tìm thấy lệnh sửa chữa.");
            }

            var existing = await _vehicleConditionLogService.GetActiveRequestAsync(serviceOrderId, type);
            if (existing != null)
            {
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.Ok(_mapper.Map<VehicleConditionLogResponseDto>(existing));
            }

            var hasCompletedLog = await _vehicleConditionLogService.HasCompletedLogAsync(serviceOrderId, type);
            if (hasCompletedLog)
            {
                var message = type == VehicleConditionLogCommandHelper.LogTypeCheckIn
                    ? "Xe đã được check-in."
                    : "Xe đã được checkout.";
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest(message);
            }

            var requestLog = await _vehicleConditionLogService.CreateRequestAsync(serviceOrderId, type);
            return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.Ok(_mapper.Map<VehicleConditionLogResponseDto>(requestLog));
        }

        public async Task<VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>> CreateAsync(
            VehicleConditionLogCreateDto? itemDto,
            CancellationToken cancellationToken)
        {
            if (itemDto == null)
            {
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest("Dữ liệu kiểm tra xe không hợp lệ.");
            }

            var logType = VehicleConditionLogCommandHelper.NormalizeLogType(itemDto.LogType);
            if (logType == "REPAIR_3D")
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest("Báo cáo sửa chữa 3D phải được tạo qua chức năng báo sửa chữa.");
            if (logType == VehicleConditionLogCommandHelper.LogTypeCheckOut)
            {
                var hasSignedCheckIn = await _vehicleConditionLogService.HasSignedCheckInAsync(itemDto.ServiceOrderId);
                if (!hasSignedCheckIn)
                {
                    return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest("Chỉ được tạo CHECK_OUT sau khi khách đã ký CHECK_IN.");
                }
            }
            else if (logType == VehicleConditionLogCommandHelper.LogTypeCheckIn)
            {
                var hasSignedCheckIn = await _vehicleConditionLogService.HasSignedCheckInAsync(itemDto.ServiceOrderId);
                if (hasSignedCheckIn)
                {
                    return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest("CHECK_IN đã được khách hàng ký xác nhận, không thể tạo mới.");
                }

                if (!string.IsNullOrWhiteSpace(itemDto.CustomerSignatureUrl) &&
                    !VehicleConditionLogCornerImageHelper.HasRequiredCornerImages(itemDto.DefectsAndImages))
                {
                    return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest(
                        "Cần chụp đủ 4 góc xe (trước trái, trước phải, sau trái, sau phải) trước khi khách ký CHECK_IN.");
                }
            }

            var entity = _mapper.Map<VehicleConditionLog>(itemDto);
            var createdItem = await _vehicleConditionLogService.AddAsync(entity);

            if (logType == VehicleConditionLogCommandHelper.LogTypeCheckIn)
            {
                await _vehicleConditionLogService.MarkActiveRequestCompletedAsync(
                    itemDto.ServiceOrderId,
                    VehicleConditionLogCommandHelper.LogTypeCheckIn);
                await VehicleConditionLogCommandHelper.TryMarkServiceOrderCheckedInAsync(
                    _vehicleConditionLogService,
                    logType,
                    itemDto.ServiceOrderId,
                    createdItem.CustomerSignatureUrl);
                await VehicleConditionLogCommandHelper.TryAutoCreateDiagnosisQueueAfterCheckInAsync(
                    _diagnosisService,
                    _queryService,
                    logType,
                    itemDto.ServiceOrderId,
                    createdItem.CustomerSignatureUrl,
                    cancellationToken);
            }
            else if (logType == VehicleConditionLogCommandHelper.LogTypeCheckOut)
            {
                await _vehicleConditionLogService.MarkActiveRequestCompletedAsync(
                    itemDto.ServiceOrderId,
                    VehicleConditionLogCommandHelper.LogTypeCheckOut);
            }

            var responseDto = _mapper.Map<VehicleConditionLogResponseDto>(createdItem);
            return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.Created(responseDto);
        }

        public async Task<VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>> UpdateAsync(
            long id,
            VehicleConditionLogUpdateDto? itemDto,
            CancellationToken cancellationToken)
        {
            var existingItem = await _vehicleConditionLogService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.NotFound();
            }

            if (itemDto == null)
            {
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest("Dữ liệu kiểm tra xe không hợp lệ.");
            }

            var existingType = VehicleConditionLogCommandHelper.NormalizeLogType(existingItem.LogType);
            if (existingType == "REPAIR_3D")
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest("Không thể chỉnh sửa trực tiếp báo cáo sửa chữa 3D.");
            if (existingType == VehicleConditionLogCommandHelper.LogTypeCheckIn &&
                !string.IsNullOrWhiteSpace(existingItem.CustomerSignatureUrl))
            {
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest("CHECK_IN đã được khách ký, không thể chỉnh sửa.");
            }

            var targetType = VehicleConditionLogCommandHelper.NormalizeLogType(itemDto.LogType);
            if (targetType == "REPAIR_3D")
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest("Không thể đổi biên bản thành báo cáo sửa chữa 3D.");
            if (string.IsNullOrWhiteSpace(targetType))
            {
                targetType = existingType;
            }

            if (targetType == VehicleConditionLogCommandHelper.LogTypeCheckOut)
            {
                var hasSignedCheckIn = await _vehicleConditionLogService.HasSignedCheckInAsync(itemDto.ServiceOrderId);
                if (!hasSignedCheckIn)
                {
                    return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest("Chỉ được cập nhật CHECK_OUT sau khi khách đã ký CHECK_IN.");
                }
            }

            var effectiveSignature = string.IsNullOrWhiteSpace(itemDto.CustomerSignatureUrl)
                ? existingItem.CustomerSignatureUrl
                : itemDto.CustomerSignatureUrl;

            var effectiveDefects = string.IsNullOrWhiteSpace(itemDto.DefectsAndImages)
                ? existingItem.DefectsAndImages
                : itemDto.DefectsAndImages;

            if (targetType == VehicleConditionLogCommandHelper.LogTypeCheckIn &&
                !string.IsNullOrWhiteSpace(effectiveSignature) &&
                !VehicleConditionLogCornerImageHelper.HasRequiredCornerImages(effectiveDefects))
            {
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.BadRequest(
                    "Cần chụp đủ 4 góc xe (trước trái, trước phải, sau trái, sau phải) trước khi khách ký CHECK_IN.");
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _vehicleConditionLogService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.NotFound();
            }

            await VehicleConditionLogCommandHelper.TryMarkServiceOrderCheckedInAsync(
                _vehicleConditionLogService,
                targetType,
                updatedItem.ServiceOrderId,
                updatedItem.CustomerSignatureUrl);
            await VehicleConditionLogCommandHelper.TryAutoCreateDiagnosisQueueAfterCheckInAsync(
                _diagnosisService,
                _queryService,
                targetType,
                updatedItem.ServiceOrderId,
                updatedItem.CustomerSignatureUrl,
                cancellationToken);

            var responseDto = _mapper.Map<VehicleConditionLogResponseDto>(updatedItem);
            return VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>.Ok(responseDto);
        }

        public async Task<VehicleConditionLogCommandResult<object>> UploadMediaAsync(
            string originalFileName,
            string contentType,
            byte[] fileBytes,
            string folderName,
            CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(originalFileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? ".mp4" : ".png";
            }

            var fileName = $"{VietnamTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
            await using var stream = new MemoryStream(fileBytes);
            var url = await _cloudStorageService.UploadAsync(
                stream,
                folderName,
                fileName,
                contentType,
                cancellationToken);

            return VehicleConditionLogCommandResult<object>.Ok(new { url });
        }
    }
}
