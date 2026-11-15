using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed record ServiceStaffAssignmentServices(
        IServiceStaffAssignmentService AssignmentService,
        IServiceStaffAssignmentQueryDataService DataService,
        IServiceStaffAssignmentBatchService BatchService,
        IServiceStaffAssignmentLifecycleService LifecycleService);

    public sealed class ServiceStaffAssignmentCommandFacade : IServiceStaffAssignmentCommandFacade
    {
        private readonly IServiceStaffAssignmentService _serviceStaffAssignmentService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IVehicleConditionLogValidationService _vehicleConditionLogService;
        private readonly IServiceStaffAssignmentQueryDataService _dataService;
        private readonly IServiceStaffAssignmentBatchService _batchService;
        private readonly IServiceStaffAssignmentLifecycleService _lifecycleService;
        private readonly IMapper _mapper;

        public ServiceStaffAssignmentCommandFacade(
            ServiceStaffAssignmentServices assignmentServices,
            IServiceOrderService serviceOrderService,
            IVehicleConditionLogValidationService vehicleConditionLogService,
            IMapper mapper)
        {
            _serviceStaffAssignmentService = assignmentServices.AssignmentService;
            _serviceOrderService = serviceOrderService;
            _vehicleConditionLogService = vehicleConditionLogService;
            _dataService = assignmentServices.DataService;
            _batchService = assignmentServices.BatchService;
            _lifecycleService = assignmentServices.LifecycleService;
            _mapper = mapper;
        }

        public async Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> CreateAsync(
            ServiceStaffAssignmentCreateDto itemDto,
            long? scopedBranchId,
            long? currentUserId)
        {
            try
            {
                var statusError = ServiceStaffAssignmentSupport.NormalizeAndValidateStatus(itemDto, isCreate: true);
                if (!string.IsNullOrWhiteSpace(statusError))
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(statusError);
                }

                var checkInValidationError = await ServiceStaffAssignmentSupport.ValidateSignedCheckInBeforeAssignmentAsync(
                    _dataService, _vehicleConditionLogService, itemDto.ServiceOrderDetailId);
                if (!string.IsNullOrWhiteSpace(checkInValidationError))
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(checkInValidationError);
                }

                var serviceOrderId = await ServiceStaffAssignmentSupport.ResolveServiceOrderIdFromDetailIdAsync(
                    _dataService, itemDto.ServiceOrderDetailId);
                if (serviceOrderId <= 0)
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(
                        "Không tìm thấy service order tương ứng với hạng mục được phân công.");
                }

                if (scopedBranchId.HasValue && !await _dataService.ServiceOrderBelongsToBranchAsync(serviceOrderId, scopedBranchId.Value))
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.Forbid();
                }

                await _serviceOrderService.EnsureAssignmentEditableAsync(serviceOrderId);

                var entity = _mapper.Map<ServiceStaffAssignment>(itemDto);
                entity.AssignedBy ??= currentUserId;
                entity.RequiresInsuranceProof = await ServiceStaffAssignmentSupport.ResolveRequiresInsuranceProofAsync(
                    _dataService, itemDto.ServiceOrderDetailId, itemDto.RequiresInsuranceProof);
                entity.ProofImageUrl = ServiceStaffAssignmentSupport.NormalizeProofImageUrl(itemDto.ProofImageUrl);

                var completionValidationError = ServiceStaffAssignmentSupport.ValidateInsuranceProofOnComplete(
                    entity.Status,
                    entity.RequiresInsuranceProof,
                    entity.ProofImageUrl);
                if (!string.IsNullOrWhiteSpace(completionValidationError))
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(completionValidationError);
                }

                var createdItem = await _serviceStaffAssignmentService.AddAsync(entity);
                var responseDto = _mapper.Map<ServiceStaffAssignmentResponseDto>(createdItem);
                return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.Created(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> UpdateAsync(
            long id,
            ServiceStaffAssignmentUpdateDto itemDto,
            long? scopedBranchId)
        {
            try
            {
                var existingItem = await _serviceStaffAssignmentService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.NotFound();
                }

                if (scopedBranchId.HasValue && !await _dataService.AssignmentBelongsToBranchAsync(id, scopedBranchId.Value))
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.Forbid();
                }

                var statusError = ServiceStaffAssignmentSupport.NormalizeAndValidateStatus(itemDto, isCreate: false);
                if (!string.IsNullOrWhiteSpace(statusError))
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(statusError);
                }

                var checkInValidationError = await ServiceStaffAssignmentSupport.ValidateSignedCheckInBeforeAssignmentAsync(
                    _dataService, _vehicleConditionLogService, itemDto.ServiceOrderDetailId);
                if (!string.IsNullOrWhiteSpace(checkInValidationError))
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(checkInValidationError);
                }

                var detailId = itemDto.ServiceOrderDetailId > 0
                    ? itemDto.ServiceOrderDetailId
                    : existingItem.ServiceOrderDetailId;
                var serviceOrderId = await ServiceStaffAssignmentSupport.ResolveServiceOrderIdFromDetailIdAsync(
                    _dataService, detailId);
                if (serviceOrderId <= 0)
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(
                        "Không tìm thấy service order tương ứng với hạng mục được phân công.");
                }

                if (scopedBranchId.HasValue && !await _dataService.ServiceOrderBelongsToBranchAsync(serviceOrderId, scopedBranchId.Value))
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.Forbid();
                }

                await _serviceOrderService.EnsureAssignmentEditableAsync(serviceOrderId);

                _mapper.Map(itemDto, existingItem);
                existingItem.RequiresInsuranceProof = await ServiceStaffAssignmentSupport.ResolveRequiresInsuranceProofAsync(
                    _dataService, detailId, itemDto.RequiresInsuranceProof);
                existingItem.ProofImageUrl = ServiceStaffAssignmentSupport.NormalizeProofImageUrl(itemDto.ProofImageUrl);

                var completionValidationError = ServiceStaffAssignmentSupport.ValidateInsuranceProofOnComplete(
                    existingItem.Status,
                    existingItem.RequiresInsuranceProof,
                    existingItem.ProofImageUrl);
                if (!string.IsNullOrWhiteSpace(completionValidationError))
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(completionValidationError);
                }

                var updatedItem = await _serviceStaffAssignmentService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.NotFound();
                }

                var responseDto = _mapper.Map<ServiceStaffAssignmentResponseDto>(updatedItem);
                return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<ServiceStaffAssignmentCommandResult> DeleteAsync(long id, long? scopedBranchId)
        {
            try
            {
                var existingItem = await _serviceStaffAssignmentService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return ServiceStaffAssignmentCommandResult.NotFound();
                }

                if (scopedBranchId.HasValue && !await _dataService.AssignmentBelongsToBranchAsync(id, scopedBranchId.Value))
                {
                    return ServiceStaffAssignmentCommandResult.Forbid();
                }

                var serviceOrderId = await ServiceStaffAssignmentSupport.ResolveServiceOrderIdFromDetailIdAsync(
                    _dataService, existingItem.ServiceOrderDetailId);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsureAssignmentEditableAsync(serviceOrderId);
                }

                var result = await _serviceStaffAssignmentService.DeleteAsync(id);
                if (!result)
                {
                    return ServiceStaffAssignmentCommandResult.NotFound();
                }

                return ServiceStaffAssignmentCommandResult.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return ServiceStaffAssignmentCommandResult.BadRequest(ex.Message);
            }
        }

        public Task<ServiceStaffAssignmentCommandResult<IEnumerable<ServiceStaffAssignmentResponseDto>>> AssignMultiAsync(
            ServiceStaffAssignmentMultiCreateDto request,
            long? scopedBranchId,
            long? currentUserId)
            => _batchService.AssignMultiAsync(request, scopedBranchId, currentUserId);

        public Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentBulkResponse>> BulkAssignAsync(
            IReadOnlyList<ServiceStaffAssignmentBulkItemInput>? assignments,
            long? scopedBranchId,
            long? currentUserId)
            => _batchService.BulkAssignAsync(assignments, scopedBranchId, currentUserId);

        public Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> StartAssignmentAsync(
            long id,
            ServiceStaffAssignmentLifecycleInput? request,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman)
            => _lifecycleService.StartAssignmentAsync(id, request, scopedBranchId, currentUserId, isManagerOrForeman);

        public Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> PauseAssignmentAsync(
            long id,
            ServiceStaffAssignmentLifecycleInput? request,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman)
            => _lifecycleService.PauseAssignmentAsync(id, request, scopedBranchId, currentUserId, isManagerOrForeman);

        public Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> CompleteAssignmentAsync(
            long id,
            ServiceStaffAssignmentLifecycleInput? request,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman)
            => _lifecycleService.CompleteAssignmentAsync(id, request, scopedBranchId, currentUserId, isManagerOrForeman);
    }
}
