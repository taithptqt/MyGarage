using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ServiceOrderCommandFacade : IServiceOrderCommandFacade
    {
        private static readonly HashSet<string> AllowedOrderCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "QUICK_SERVICE",
            "GENERAL_REPAIR",
            "MAINTENANCE",
            "INSURANCE_REPAIR",
            "REWORK",
            "WARRANTLY",
        };

        private readonly IServiceOrderService _serviceOrderService;
        private readonly ICloudStorageService _cloudStorageService;
        private readonly IServiceOrderDataService _dataService;
        private readonly IServiceOrderEditRequestService _editRequestService;
        private readonly IMapper _mapper;

        public ServiceOrderCommandFacade(
            IServiceOrderService serviceOrderService,
            ICloudStorageService cloudStorageService,
            IServiceOrderDataService dataService,
            IServiceOrderEditRequestService editRequestService,
            IMapper mapper)
        {
            _serviceOrderService = serviceOrderService;
            _cloudStorageService = cloudStorageService;
            _dataService = dataService;
            _editRequestService = editRequestService;
            _mapper = mapper;
        }

        internal ServiceOrderCommandFacade(
            IServiceOrderService serviceOrderService,
            IAuditLogService auditLogService,
            ICloudStorageService cloudStorageService,
            IServiceOrderDataService dataService,
            IMapper mapper)
            : this(
                serviceOrderService,
                cloudStorageService,
                dataService,
                new ServiceOrderEditRequestService(serviceOrderService, auditLogService, dataService),
                mapper)
        {
        }

        public async Task<PagedEnvelope<ServiceOrderResponseDto>> GetPagedAsync(
            long? scopedBranchId,
            string? status,
            int page,
            int pageSize)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _dataService.GetPagedAsync(scopedBranchId, status, normalizedPage, normalizedSize);
            var dtos = await MapServiceOrderResponsesAsync(paged);
            return PagedEnvelope<ServiceOrderResponseDto>.Create(dtos, total, normalizedPage, normalizedSize);
        }

        public async Task<ServiceOrderResponseDto?> GetByIdAsync(long id)
        {
            var item = await _serviceOrderService.GetByIdAsync(id);
            if (item == null)
            {
                return null;
            }

            return await MapServiceOrderResponseAsync(item);
        }

        public async Task<ServiceOrderCommandResult<ServiceOrderResponseDto>> CreateAsync(
            ServiceOrderCreateDto itemDto,
            long? actorUserId)
        {
            var validationError = NormalizeAndValidateFields(itemDto);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return ServiceOrderCommandResult<ServiceOrderResponseDto>.BadRequest(validationError);
            }

            var entity = _mapper.Map<ServiceOrder>(itemDto);
            entity.Status = nameof(ServiceOrderLifecycleStatus.NEW);
            entity.CreatedBy = actorUserId;
            entity.UpdatedBy = actorUserId;

            var createdItem = await _serviceOrderService.AddAsync(entity);
            var responseDto = await MapServiceOrderResponseAsync(createdItem);
            return ServiceOrderCommandResult<ServiceOrderResponseDto>.Created(responseDto);
        }

        public async Task<ServiceOrderCommandResult<ServiceOrderBatchCreateResponseDto>> CreateBatchAsync(
            ServiceOrderBatchCreateRequestDto dto,
            long? actorUserId)
        {
            try
            {
                var result = await _serviceOrderService.AddWorkshopBatchAsync(actorUserId, dto);
                var mappedOrders = await MapServiceOrderResponsesAsync(result.Orders);
                var response = new ServiceOrderBatchCreateResponseDto
                {
                    GroupSessionId = result.GroupSessionId,
                    TotalCreated = mappedOrders.Count,
                    Orders = mappedOrders
                };

                return ServiceOrderCommandResult<ServiceOrderBatchCreateResponseDto>.Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return ServiceOrderCommandResult<ServiceOrderBatchCreateResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<ServiceOrderCommandResult<ServiceOrderResponseDto>> UpdateWorkshopAsync(
            long id,
            WorkshopServiceOrderEditRequestDto dto,
            long? actorUserId)
        {
            try
            {
                var updatedItem = await _serviceOrderService.UpdateWorkshopAsync(id, dto, actorUserId);
                if (updatedItem == null)
                {
                    return ServiceOrderCommandResult<ServiceOrderResponseDto>.NotFound();
                }

                var responseDto = await MapServiceOrderResponseAsync(updatedItem);
                return ServiceOrderCommandResult<ServiceOrderResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return ServiceOrderCommandResult<ServiceOrderResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<ServiceOrderCommandResult<ServiceOrderResponseDto>> UpdateAsync(
            long id,
            ServiceOrderUpdateDto itemDto,
            long? actorUserId)
        {
            try
            {
                var existingItem = await _serviceOrderService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return ServiceOrderCommandResult<ServiceOrderResponseDto>.NotFound();
                }

                await _serviceOrderService.EnsureWorkshopEditableAsync(id);

                var validationError = NormalizeAndValidateFields(itemDto);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    return ServiceOrderCommandResult<ServiceOrderResponseDto>.BadRequest(validationError);
                }

                _mapper.Map(itemDto, existingItem);
                existingItem.UpdatedBy = actorUserId;
                var updatedItem = await _serviceOrderService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return ServiceOrderCommandResult<ServiceOrderResponseDto>.NotFound();
                }

                var responseDto = await MapServiceOrderResponseAsync(updatedItem);
                return ServiceOrderCommandResult<ServiceOrderResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return ServiceOrderCommandResult<ServiceOrderResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<ServiceOrderCommandResult> DeleteAsync(long id)
        {
            try
            {
                var existingItem = await _serviceOrderService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return ServiceOrderCommandResult.NotFound();
                }

                await _serviceOrderService.EnsureWorkshopEditableAsync(id);

                var result = await _serviceOrderService.DeleteAsync(id);
                if (!result)
                {
                    return ServiceOrderCommandResult.NotFound();
                }

                return ServiceOrderCommandResult.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return ServiceOrderCommandResult.BadRequest(ex.Message);
            }
        }

        public async Task<ServiceOrderCommandResult<object>> UploadInsuranceAssetAsync(
            string fileName,
            string contentType,
            byte[] fileBytes,
            string? assetType,
            CancellationToken cancellationToken)
        {
            var normalizedAssetType = NormalizeInsuranceAssetType(assetType);
            if (normalizedAssetType == null)
            {
                return ServiceOrderCommandResult<object>.BadRequest("Loại ảnh bảo hiểm không hợp lệ.");
            }

            try
            {
                var uploaded = await SaveInsuranceAssetFileAsync(fileName, contentType, fileBytes, normalizedAssetType, cancellationToken);
                await _dataService.AddInsuranceUploadInTransactionAsync(new ServiceOrderInsuranceUpload
                {
                    AssetType = normalizedAssetType,
                    BlobPath = uploaded.BlobPath,
                    FileUrl = uploaded.Url,
                    Status = ServiceOrderInsuranceUploadStatuses.Staged
                });

                return ServiceOrderCommandResult<object>.Ok(new
                {
                    url = uploaded.Url,
                    assetType = normalizedAssetType
                });
            }
            catch
            {
                return ServiceOrderCommandResult<object>.BadRequest("Không thể tải ảnh bảo hiểm lên máy chủ.");
            }
        }

        public Task<IReadOnlyList<ServiceOrderEditRequestItemDto>> GetEditRequestsAsync(long id, string? status)
            => _editRequestService.GetEditRequestsAsync(id, status);

        public Task<ServiceOrderCommandResult> RequestEditAsync(
            long id,
            long? currentUserId,
            EditRequestCreateDto? dto)
            => _editRequestService.RequestEditAsync(id, currentUserId, dto);

        public Task<ServiceOrderCommandResult> DecideEditRequestAsync(
            long id,
            long requestId,
            long? currentUserId,
            string? claimRoleCode,
            string? claimRoleName,
            EditRequestDecisionDto? dto)
            => _editRequestService.DecideEditRequestAsync(
                id,
                requestId,
                currentUserId,
                claimRoleCode,
                claimRoleName,
                dto);

        private async Task<List<ServiceOrderResponseDto>> MapServiceOrderResponsesAsync(IEnumerable<ServiceOrder> items)
        {
            var serviceOrders = (items ?? Enumerable.Empty<ServiceOrder>()).ToList();
            if (serviceOrders.Count == 0)
            {
                return new List<ServiceOrderResponseDto>();
            }

            var orderIds = serviceOrders.Select(item => item.Id).Where(id => id > 0).Distinct().ToList();
            var vehicleIds = serviceOrders.Select(item => item.VehicleId).Where(id => id > 0).Distinct().ToList();
            var batchData = await _dataService.GetBatchMappingDataAsync(orderIds, vehicleIds);

            return serviceOrders
                .Select(item => MapServiceOrderResponse(
                    item,
                    batchData.VehicleById.TryGetValue(item.VehicleId, out var vehicle) ? vehicle : null,
                    batchData.AssetsByOrderId.TryGetValue(item.Id, out var assets) ? assets : null,
                    batchData.InsuranceCompanyByOrderId.TryGetValue(item.Id, out var insuranceCompanyId) ? insuranceCompanyId : null,
                    batchData.DiscountByOrderId.TryGetValue(item.Id, out var totalDiscount) ? totalDiscount : 0m))
                .ToList();
        }

        private async Task<ServiceOrderResponseDto> MapServiceOrderResponseAsync(ServiceOrder item)
        {
            var singleData = await _dataService.GetSingleMappingDataAsync(item.Id, item.VehicleId);
            return MapServiceOrderResponse(
                item,
                singleData.Vehicle,
                singleData.Assets,
                singleData.InsuranceCompanyId,
                singleData.TotalDiscount);
        }

        private ServiceOrderResponseDto MapServiceOrderResponse(
            ServiceOrder item,
            ServiceOrderVehicleSnapshot? vehicle,
            IReadOnlyCollection<ServiceOrderInsuranceAsset>? assets,
            long? insuranceCompanyId,
            decimal totalDiscount)
        {
            var dto = _mapper.Map<ServiceOrderResponseDto>(item);
            dto.VehicleLicensePlate = vehicle?.LicensePlate;
            dto.VehicleModelId = vehicle?.ModelId;
            dto.VehicleModelName = vehicle?.ModelName;
            dto.VehicleBrandId = vehicle?.BrandId;
            dto.VehicleBrandName = vehicle?.BrandName;
            dto.InsuranceCompanyId = insuranceCompanyId > 0 ? insuranceCompanyId : null;
            dto.TotalDiscount = totalDiscount;
            dto.InsuranceCardImageUrls = assets?
                .Where(asset => string.Equals(asset.AssetType, AppointmentInsuranceAssetTypes.Card, StringComparison.OrdinalIgnoreCase))
                .Select(asset => asset.FileUrl)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToList() ?? new List<string>();
            dto.InsuranceDamageImageUrls = assets?
                .Where(asset => string.Equals(asset.AssetType, AppointmentInsuranceAssetTypes.Damage, StringComparison.OrdinalIgnoreCase))
                .Select(asset => asset.FileUrl)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToList() ?? new List<string>();
            return dto;
        }

        private async Task<(string Url, string BlobPath)> SaveInsuranceAssetFileAsync(
            string originalFileName,
            string contentType,
            byte[] fileBytes,
            string assetType,
            CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(originalFileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var folderName = string.Equals(assetType, AppointmentInsuranceAssetTypes.Card, StringComparison.OrdinalIgnoreCase)
                ? "service-orders/insurance-cards"
                : "service-orders/insurance-damage";
            var fileName = $"{VietnamTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
            var blobPath = $"{folderName.Trim('/')}/{fileName}";
            await using var stream = new MemoryStream(fileBytes);
            var url = await _cloudStorageService.UploadAsync(
                stream,
                folderName,
                fileName,
                contentType,
                cancellationToken);
            return (url, blobPath);
        }

        private static string? NormalizeInsuranceAssetType(string? assetType)
        {
            var normalized = string.IsNullOrWhiteSpace(assetType)
                ? string.Empty
                : assetType.Trim().ToUpperInvariant();

            return normalized switch
            {
                AppointmentInsuranceAssetTypes.Card => AppointmentInsuranceAssetTypes.Card,
                AppointmentInsuranceAssetTypes.Damage => AppointmentInsuranceAssetTypes.Damage,
                _ => null
            };
        }

        private static string? NormalizeAndValidateFields(ServiceOrderCreateDto dto)
        {
            if (dto == null)
            {
                return "Dữ liệu Service Order không hợp lệ.";
            }

            var error = TryNormalizeOrderCategory(dto.OrderCategory, out var normalizedOrderCategory);
            if (error != null) return error;
            if (normalizedOrderCategory != null) dto.OrderCategory = normalizedOrderCategory;

            return null;
        }

        private static string? NormalizeAndValidateFields(ServiceOrderUpdateDto dto)
        {
            if (dto == null)
            {
                return "Dữ liệu Service Order không hợp lệ.";
            }

            var error = TryNormalizeOrderCategory(dto.OrderCategory, out var normalizedOrderCategory);
            if (error != null) return error;
            if (normalizedOrderCategory != null) dto.OrderCategory = normalizedOrderCategory;

            return null;
        }

        private static string? TryNormalizeOrderCategory(string? rawValue, out string? normalizedValue)
        {
            normalizedValue = null;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var token = ServiceOrderValueHelper.NormalizeToken(rawValue) ?? string.Empty;
            if (!AllowedOrderCategories.Contains(token))
            {
                var allowed = string.Join(", ", AllowedOrderCategories.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                return $"Giá trị orderCategory không hợp lệ: {rawValue}. Chấp nhận: {allowed}.";
            }

            normalizedValue = token;
            return null;
        }
    }
}
