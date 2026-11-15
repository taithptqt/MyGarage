using System;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class ServiceStaffAssignmentLifecycleService : IServiceStaffAssignmentLifecycleService
    {
        private readonly IServiceStaffAssignmentLifecycleDataService _dataService;
        private readonly IMapper _mapper;

        public ServiceStaffAssignmentLifecycleService(
            IServiceStaffAssignmentLifecycleDataService dataService,
            IMapper mapper)
        {
            _dataService = dataService;
            _mapper = mapper;
        }

        public async Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> StartAssignmentAsync(
            long id,
            ServiceStaffAssignmentLifecycleInput? request,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman)
        {
            var (assignment, error) = await LoadAuthorizedAssignmentAsync(id, scopedBranchId, currentUserId, isManagerOrForeman);
            if (error != null)
            {
                return error;
            }

            var currentStatus = ServiceStaffAssignmentSupport.NormalizeStatus(assignment!.Status);
            if (string.IsNullOrWhiteSpace(currentStatus))
            {
                currentStatus = nameof(ServiceProgressStatus.PENDING);
            }

            if (currentStatus is not nameof(ServiceProgressStatus.PENDING) and not nameof(ServiceProgressStatus.PAUSED))
            {
                return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(
                    $"Chỉ có thể bắt đầu tác vụ ở trạng thái PENDING hoặc PAUSED (hiện tại: {assignment.Status}).");
            }

            var now = VietnamTime.Now;
            assignment.ActualStartTime ??= request?.StartedAt?.ToLocalTime() ?? now;
            if (currentStatus == nameof(ServiceProgressStatus.PAUSED))
            {
                var resumeLog = $"[RESUME {now:dd/MM/yyyy HH:mm}]";
                assignment.PauseLog = string.IsNullOrWhiteSpace(assignment.PauseLog)
                    ? resumeLog
                    : $"{assignment.PauseLog}\n{resumeLog}";
            }

            assignment.Status = nameof(ServiceProgressStatus.IN_PROGRESS);
            assignment.UpdatedAt = now;
            assignment.UpdatedBy = currentUserId;

            var so = assignment.ServiceOrderDetail.ServiceOrder;
            var soStatus = ServiceStaffAssignmentSupport.NormalizeStatus(so.Status);
            if (soStatus is nameof(ServiceOrderLifecycleStatus.APPROVED) or nameof(ServiceOrderLifecycleStatus.WAITING) or nameof(ServiceOrderLifecycleStatus.WAITING_PARTS))
            {
                so.Status = nameof(ServiceOrderLifecycleStatus.IN_PROGRESS);
                so.UpdatedAt = now;
            }

            await _dataService.SaveChangesAsync();
            return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.Ok(
                _mapper.Map<ServiceStaffAssignmentResponseDto>(assignment));
        }

        public async Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> PauseAssignmentAsync(
            long id,
            ServiceStaffAssignmentLifecycleInput? request,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman)
        {
            var (assignment, error) = await LoadAuthorizedAssignmentAsync(id, scopedBranchId, currentUserId, isManagerOrForeman);
            if (error != null)
            {
                return error;
            }

            var currentStatus = ServiceStaffAssignmentSupport.NormalizeStatus(assignment!.Status);
            if (currentStatus != nameof(ServiceProgressStatus.IN_PROGRESS))
            {
                return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(
                    $"Chỉ có thể tạm dừng tác vụ đang IN_PROGRESS (hiện tại: {assignment.Status}).");
            }

            var now = VietnamTime.Now;
            var note = string.IsNullOrWhiteSpace(request?.Note) ? string.Empty : $" - {request.Note.Trim()}";
            var pauseEntry = $"[PAUSE {now:dd/MM/yyyy HH:mm}{note}]";
            assignment.PauseLog = string.IsNullOrWhiteSpace(assignment.PauseLog)
                ? pauseEntry
                : $"{assignment.PauseLog}\n{pauseEntry}";
            assignment.Status = nameof(ServiceProgressStatus.PAUSED);
            assignment.UpdatedAt = now;
            assignment.UpdatedBy = currentUserId;

            await _dataService.SaveChangesAsync();
            return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.Ok(
                _mapper.Map<ServiceStaffAssignmentResponseDto>(assignment));
        }

        public async Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> CompleteAssignmentAsync(
            long id,
            ServiceStaffAssignmentLifecycleInput? request,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman)
        {
            var (assignment, error) = await LoadAuthorizedAssignmentAsync(id, scopedBranchId, currentUserId, isManagerOrForeman);
            if (error != null)
            {
                return error;
            }

            var currentStatus = ServiceStaffAssignmentSupport.NormalizeStatus(assignment!.Status);
            if (currentStatus == nameof(ServiceProgressStatus.COMPLETED))
            {
                return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(
                    "Tác vụ này đã hoàn thành trước đó.");
            }

            if (currentStatus is not nameof(ServiceProgressStatus.IN_PROGRESS) and not nameof(ServiceProgressStatus.PAUSED))
            {
                return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(
                    $"Cần bắt đầu tác vụ trước khi hoàn thành (hiện tại: {assignment.Status}).");
            }

            var normalizedProof = ServiceStaffAssignmentSupport.NormalizeProofImageUrl(request?.ProofImageUrl) ?? assignment.ProofImageUrl;
            var proofError = ServiceStaffAssignmentSupport.ValidateInsuranceProofOnComplete(
                nameof(ServiceProgressStatus.COMPLETED),
                assignment.RequiresInsuranceProof,
                normalizedProof);
            if (!string.IsNullOrWhiteSpace(proofError))
            {
                return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.BadRequest(proofError);
            }

            var now = VietnamTime.Now;
            assignment.ActualStartTime ??= now;
            assignment.ActualEndTime = request?.CompletedAt?.ToLocalTime() ?? now;
            assignment.ProofImageUrl = normalizedProof;
            assignment.Status = nameof(ServiceProgressStatus.COMPLETED);
            assignment.UpdatedAt = now;
            assignment.UpdatedBy = currentUserId;

            await _dataService.SaveChangesAsync();
            return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.Ok(
                _mapper.Map<ServiceStaffAssignmentResponseDto>(assignment));
        }

        private async Task<(ServiceStaffAssignment? Assignment, ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>? Error)> LoadAuthorizedAssignmentAsync(
            long id,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman)
        {
            var assignment = await _dataService.GetAssignmentWithOrderAsync(id);
            if (assignment == null)
            {
                return (null, ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.NotFound(
                    "Không tìm thấy phân công tác vụ."));
            }

            if (scopedBranchId.HasValue && assignment.ServiceOrderDetail.ServiceOrder.ExecuteBranchId != scopedBranchId.Value)
            {
                return (null, ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.Forbid());
            }

            var canOperate = (currentUserId.HasValue && assignment.TechnicianId == currentUserId.Value) || isManagerOrForeman;
            if (!canOperate)
            {
                return (null, ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>.Forbid());
            }

            return (assignment, null);
        }
    }
}
