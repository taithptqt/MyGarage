using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class ServiceStaffAssignmentBatchService : IServiceStaffAssignmentBatchService
    {
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IVehicleConditionLogValidationService _vehicleConditionLogService;
        private readonly IServiceStaffAssignmentQueryDataService _queryDataService;
        private readonly IServiceStaffAssignmentRosterDataService _rosterDataService;
        private readonly IMapper _mapper;

        public ServiceStaffAssignmentBatchService(
            IServiceOrderService serviceOrderService,
            IVehicleConditionLogValidationService vehicleConditionLogService,
            IServiceStaffAssignmentQueryDataService queryDataService,
            IServiceStaffAssignmentRosterDataService rosterDataService,
            IMapper mapper)
        {
            _serviceOrderService = serviceOrderService;
            _vehicleConditionLogService = vehicleConditionLogService;
            _queryDataService = queryDataService;
            _rosterDataService = rosterDataService;
            _mapper = mapper;
        }

        public async Task<ServiceStaffAssignmentCommandResult<IEnumerable<ServiceStaffAssignmentResponseDto>>> AssignMultiAsync(
            ServiceStaffAssignmentMultiCreateDto request,
            long? scopedBranchId,
            long? currentUserId)
        {
            try
            {
                var (createdRows, errorResult) = await BuildMultiAssignmentRowsForDetailAsync(request, scopedBranchId, currentUserId);
                if (errorResult != null)
                {
                    return new ServiceStaffAssignmentCommandResult<IEnumerable<ServiceStaffAssignmentResponseDto>>(
                        errorResult.StatusCode,
                        null,
                        errorResult.Payload);
                }

                await _rosterDataService.AddAssignmentsAsync(createdRows!);

                var response = _mapper.Map<List<ServiceStaffAssignmentResponseDto>>(createdRows!);
                return ServiceStaffAssignmentCommandResult<IEnumerable<ServiceStaffAssignmentResponseDto>>.Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return ServiceStaffAssignmentCommandResult<IEnumerable<ServiceStaffAssignmentResponseDto>>.BadRequest(ex.Message);
            }
        }

        public async Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentBulkResponse>> BulkAssignAsync(
            IReadOnlyList<ServiceStaffAssignmentBulkItemInput>? assignments,
            long? scopedBranchId,
            long? currentUserId)
        {
            if (assignments == null || assignments.Count == 0)
            {
                return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentBulkResponse>.BadRequest(
                    "Danh sách phân công kỹ thuật viên không được trống.");
            }

            try
            {
                var grouped = assignments
                    .Where(x => x != null)
                    .GroupBy(x => x.ServiceOrderDetailId)
                    .ToList();

                var allCreatedRows = new List<ServiceStaffAssignment>();
                foreach (var group in grouped)
                {
                    var subRequest = new ServiceStaffAssignmentMultiCreateDto
                    {
                        ServiceOrderDetailId = group.Key,
                        AssignedBy = group.Select(x => x.AssignedBy).FirstOrDefault(x => x.HasValue),
                        Assignments = group.Select(x => new ServiceStaffAssignmentMemberDto
                        {
                            TechnicianId = x.TechnicianId ?? x.StaffId ?? 0,
                            RoleInTask = x.RoleInTask,
                            SplitRatio = x.SplitRatio,
                            EstimatedCommission = x.EstimatedCommission,
                            Status = x.Status,
                            RequiresInsuranceProof = x.RequiresInsuranceProof,
                            ProofImageUrl = x.ProofImageUrl,
                            IsRework = x.IsRework
                        }).ToList()
                    };

                    var (rows, errorResult) = await BuildMultiAssignmentRowsForDetailAsync(subRequest, scopedBranchId, currentUserId);
                    if (errorResult != null)
                    {
                        return new ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentBulkResponse>(
                            errorResult.StatusCode,
                            null,
                            errorResult.Payload);
                    }

                    if (rows != null)
                    {
                        allCreatedRows.AddRange(rows);
                    }
                }

                await _rosterDataService.AddAssignmentsAsync(allCreatedRows);

                var response = _mapper.Map<List<ServiceStaffAssignmentResponseDto>>(allCreatedRows);
                return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentBulkResponse>.Ok(
                    new ServiceStaffAssignmentBulkResponse(response.Count, response));
            }
            catch (InvalidOperationException ex)
            {
                return ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentBulkResponse>.BadRequest(ex.Message);
            }
        }

        private async Task<(List<ServiceStaffAssignment>? Rows, ServiceStaffAssignmentCommandResult? ErrorResult)> BuildMultiAssignmentRowsForDetailAsync(
            ServiceStaffAssignmentMultiCreateDto request,
            long? scopedBranchId,
            long? currentUserId)
        {
            if (request == null || request.ServiceOrderDetailId <= 0 || request.Assignments == null || request.Assignments.Count == 0)
            {
                return (null, ServiceStaffAssignmentCommandResult.BadRequest("Thiếu dữ liệu phân công nhiều thợ."));
            }

            var checkInValidationError = await ServiceStaffAssignmentSupport.ValidateSignedCheckInBeforeAssignmentAsync(
                _queryDataService, _vehicleConditionLogService, request.ServiceOrderDetailId);
            if (!string.IsNullOrWhiteSpace(checkInValidationError))
            {
                return (null, ServiceStaffAssignmentCommandResult.BadRequest(checkInValidationError));
            }

            var serviceOrderId = await ServiceStaffAssignmentSupport.ResolveServiceOrderIdFromDetailIdAsync(
                _queryDataService, request.ServiceOrderDetailId);
            if (serviceOrderId <= 0)
            {
                return (null, ServiceStaffAssignmentCommandResult.BadRequest(
                    "Không tìm thấy service order tương ứng với hạng mục được phân công."));
            }

            if (scopedBranchId.HasValue && !await _queryDataService.ServiceOrderBelongsToBranchAsync(serviceOrderId, scopedBranchId.Value))
            {
                return (null, ServiceStaffAssignmentCommandResult.Forbid());
            }

            await _serviceOrderService.EnsureAssignmentEditableAsync(serviceOrderId);

            var detailInfo = await _rosterDataService.GetDetailInfoAsync(request.ServiceOrderDetailId);
            if (detailInfo == null)
            {
                return (null, ServiceStaffAssignmentCommandResult.BadRequest("Không tìm thấy hạng mục dịch vụ cần phân công."));
            }

            var technicianIds = request.Assignments
                .Where(x => x != null && x.TechnicianId > 0)
                .Select(x => x.TechnicianId)
                .Distinct()
                .ToList();
            if (technicianIds.Count == 0)
            {
                return (null, ServiceStaffAssignmentCommandResult.BadRequest("Danh sách kỹ thuật viên không hợp lệ."));
            }

            var duplicateTechnicians = request.Assignments
                .Where(x => x != null && x.TechnicianId > 0)
                .GroupBy(x => x.TechnicianId)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToList();
            if (duplicateTechnicians.Count > 0)
            {
                return (null, ServiceStaffAssignmentCommandResult.BadRequest(
                    $"Kỹ thuật viên bị trùng trong cùng hạng mục: {string.Join(", ", duplicateTechnicians)}."));
            }

            var existingAssignedTechnicianIds = await _rosterDataService.GetExistingAssignedTechnicianIdsAsync(
                request.ServiceOrderDetailId,
                technicianIds);
            if (existingAssignedTechnicianIds.Count > 0)
            {
                return (null, ServiceStaffAssignmentCommandResult.BadRequest(
                    $"Kỹ thuật viên đã được giao trước đó: {string.Join(", ", existingAssignedTechnicianIds)}."));
            }

            var technicians = await _rosterDataService.GetTechniciansByIdsAsync(technicianIds);

            var missingTechnicianIds = technicianIds.Where(x => !technicians.ContainsKey(x)).ToList();
            if (missingTechnicianIds.Count > 0)
            {
                return (null, ServiceStaffAssignmentCommandResult.BadRequest(
                    $"Không tìm thấy kỹ thuật viên: {string.Join(", ", missingTechnicianIds)}."));
            }

            if (scopedBranchId.HasValue && technicians.Values.Any(t => t.BranchId != scopedBranchId.Value))
            {
                return (null, ServiceStaffAssignmentCommandResult.Forbid());
            }

            var requiredSpecialty = ServiceStaffAssignmentSupport.ResolveRequiredSpecialty(detailInfo.ItemName, detailInfo.Description);
            if (!string.Equals(requiredSpecialty, "GENERAL", StringComparison.OrdinalIgnoreCase))
            {
                var invalidSpecialtyRows = new List<string>();
                foreach (var assignment in request.Assignments)
                {
                    var technician = technicians[assignment.TechnicianId];
                    if (!ServiceStaffAssignmentSupport.TechnicianSupportsSpecialty(technician.Specialty, requiredSpecialty))
                    {
                        invalidSpecialtyRows.Add($"{technician.FullName}({assignment.TechnicianId})");
                    }
                }

                if (invalidSpecialtyRows.Count > 0)
                {
                    return (null, ServiceStaffAssignmentCommandResult.BadRequest(new
                    {
                        message = $"Chuyên môn kỹ thuật viên không phù hợp hạng mục ({requiredSpecialty}).",
                        invalidTechnicians = invalidSpecialtyRows
                    }));
                }
            }

            var splitRatios = new List<decimal>();
            foreach (var assignment in request.Assignments)
            {
                var ratio = assignment.SplitRatio ?? (1m / request.Assignments.Count);
                if (ratio <= 0m || ratio > 1m)
                {
                    return (null, ServiceStaffAssignmentCommandResult.BadRequest(
                        $"SplitRatio không hợp lệ cho kỹ thuật viên {assignment.TechnicianId}."));
                }
                splitRatios.Add(ratio);
            }

            var totalRatio = splitRatios.Sum();
            if (totalRatio > 1.0001m)
            {
                return (null, ServiceStaffAssignmentCommandResult.BadRequest(
                    $"Tổng SplitRatio vượt quá 1.0 ({totalRatio:0.####})."));
            }

            var actorId = currentUserId ?? request.AssignedBy;
            var now = VietnamTime.Now;
            var createdRows = new List<ServiceStaffAssignment>();

            for (var i = 0; i < request.Assignments.Count; i++)
            {
                var item = request.Assignments[i];
                var status = ServiceStaffAssignmentSupport.NormalizeStatus(item.Status);
                if (string.IsNullOrWhiteSpace(status))
                {
                    status = nameof(ServiceProgressStatus.PENDING);
                }
                if (!ServiceStaffAssignmentSupport.AllowedStatuses.Contains(status))
                {
                    var allowed = string.Join(", ", ServiceStaffAssignmentSupport.AllowedStatuses.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                    return (null, ServiceStaffAssignmentCommandResult.BadRequest(
                        $"Giá trị status không hợp lệ: {item.Status}. Chấp nhận: {allowed}."));
                }

                var proofImageUrl = ServiceStaffAssignmentSupport.NormalizeProofImageUrl(item.ProofImageUrl);
                var requiresInsuranceProof = item.RequiresInsuranceProof ||
                    string.Equals(detailInfo.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(detailInfo.PayerType, "SPLIT", StringComparison.OrdinalIgnoreCase);
                var completionValidationError = ServiceStaffAssignmentSupport.ValidateInsuranceProofOnComplete(
                    status, requiresInsuranceProof, proofImageUrl);
                if (!string.IsNullOrWhiteSpace(completionValidationError))
                {
                    return (null, ServiceStaffAssignmentCommandResult.BadRequest(completionValidationError));
                }

                createdRows.Add(new ServiceStaffAssignment
                {
                    ServiceOrderDetailId = request.ServiceOrderDetailId,
                    AssignedBy = actorId,
                    TechnicianId = item.TechnicianId,
                    RoleInTask = string.IsNullOrWhiteSpace(item.RoleInTask) ? "ASSISTANT" : item.RoleInTask.Trim(),
                    SplitRatio = splitRatios[i],
                    EstimatedCommission = item.EstimatedCommission,
                    Status = status,
                    IsRework = item.IsRework,
                    RequiresInsuranceProof = requiresInsuranceProof,
                    ProofImageUrl = proofImageUrl,
                    CreatedAt = now,
                    UpdatedAt = now,
                    UpdatedBy = actorId
                });
            }

            return (createdRows, null);
        }
    }
}
