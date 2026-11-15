using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.DiagnosisCommandHelpers;

namespace MyGarage.Services
{
    public class DiagnosisResponseCommandService : IDiagnosisResponseCommandService
    {
        private readonly IDiagnosisService _diagnosisService;
        private readonly IMapper _mapper;

        public DiagnosisResponseCommandService(
            IDiagnosisService diagnosisService,
            IMapper mapper)
        {
            _diagnosisService = diagnosisService;
            _mapper = mapper;
        }

        public async Task<IReadOnlyList<DiagnosisResponseDto>> GetAllAsync(long? serviceOrderId, bool requestOnly, long? teamId)
        {
            var items = await _diagnosisService.GetAllFilteredAsync(serviceOrderId, requestOnly, teamId);
            var mappedDtos = _mapper.Map<IEnumerable<DiagnosisResponseDto>>(items).ToList();
            for (var i = 0; i < mappedDtos.Count && i < items.Count; i++)
            {
                ApplyAssignmentStateToDto(items[i], mappedDtos[i]);
            }

            await ApplyAssignmentVersionsToDtosAsync(items, mappedDtos);
            return await EnrichDiagnosisDtosAsync(mappedDtos);
        }

        public async Task<DiagnosisResponseDto?> GetByIdAsync(long id)
        {
            var item = await _diagnosisService.GetDiagnosisEntityAsync(id);
            if (item == null)
            {
                return null;
            }

            var dto = _mapper.Map<DiagnosisResponseDto>(item);
            ApplyAssignmentStateToDto(item, dto);
            await ApplyAssignmentVersionsToDtosAsync(new[] { item }, new[] { dto });
            return await EnrichDiagnosisDtoAsync(dto, new Dictionary<long, Team?>(), new Dictionary<long, Staff?>());
        }

        public async Task<DiagnosisResponseDto> CreateAsync(DiagnosisCreateDto itemDto)
        {
            itemDto.Status = NormalizeStatus(itemDto.Status);
            itemDto.DispatchTag = NormalizeDispatchTag(itemDto.DispatchTag);

            var entity = _mapper.Map<Diagnosis>(itemDto);
            var createdItem = await _diagnosisService.AddAsync(entity);
            return await BuildSingleResponseDtoAsync(createdItem);
        }

        public async Task<DiagnosisResponseDto?> UpdateAsync(long id, DiagnosisUpdateDto itemDto)
        {
            var existingItem = await _diagnosisService.GetDiagnosisEntityAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            var status = NormalizeStatus(itemDto.Status ?? existingItem.Status ?? DiagnosisStatusPending);

            _mapper.Map(itemDto, existingItem);
            existingItem.Status = status;
            existingItem.DispatchTag = NormalizeDispatchTag(existingItem.DispatchTag);
            existingItem.UpdatedAt = VietnamTime.Now;

            var updatedItem = await _diagnosisService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return null;
            }

            return await BuildSingleResponseDtoAsync(updatedItem);
        }

        public async Task<DiagnosisCommandResult<DiagnosisResponseDto>> CompleteAsync(
            long id,
            string? symptom,
            string? rootCause,
            string? severity)
        {
            var existingItem = await _diagnosisService.GetDiagnosisEntityAsync(id);
            if (existingItem == null)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.NotFound("Không tìm thấy phiên chẩn đoán.");
            }

            if (!string.IsNullOrWhiteSpace(symptom))
            {
                existingItem.Symptom = symptom;
            }

            if (!string.IsNullOrWhiteSpace(rootCause))
            {
                existingItem.RootCause = rootCause;
            }

            if (!string.IsNullOrWhiteSpace(severity))
            {
                existingItem.Severity = severity;
            }

            existingItem.Status = DiagnosisStatusCompleted;
            await _diagnosisService.CompleteDiagnosisAsync(existingItem);

            var order = await _diagnosisService.GetServiceOrderAsync(existingItem.ServiceOrderId);
            if (order != null)
            {
                await _diagnosisService.UpdateServiceOrderStatusAsync(order, ServiceOrderStatusDiagnosing);
            }

            var dto = await BuildSingleResponseDtoAsync(existingItem);
            return DiagnosisCommandResult<DiagnosisResponseDto>.Ok(dto);
        }

        public async Task<DiagnosisResponseDto> BuildSingleResponseDtoAsync(Diagnosis entity, bool applyAssignmentState = true)
        {
            var dto = _mapper.Map<DiagnosisResponseDto>(entity);
            if (applyAssignmentState)
            {
                ApplyAssignmentStateToDto(entity, dto);
            }

            return await EnrichDiagnosisDtoAsync(dto, new Dictionary<long, Team?>(), new Dictionary<long, Staff?>());
        }

        private async Task<List<DiagnosisResponseDto>> EnrichDiagnosisDtosAsync(IEnumerable<DiagnosisResponseDto> source)
        {
            var list = source?.ToList() ?? new List<DiagnosisResponseDto>();
            if (list.Count == 0)
            {
                return list;
            }

            var teamCache = new Dictionary<long, Team?>();
            var staffCache = new Dictionary<long, Staff?>();
            foreach (var dto in list)
            {
                await EnrichDiagnosisDtoAsync(dto, teamCache, staffCache);
            }

            return list;
        }

        private async Task<DiagnosisResponseDto> EnrichDiagnosisDtoAsync(
            DiagnosisResponseDto dto,
            IDictionary<long, Team?> teamCache,
            IDictionary<long, Staff?> staffCache)
        {
            long? resolvedTeamId = dto.AssignedTeamId;
            if ((!resolvedTeamId.HasValue || resolvedTeamId.Value <= 0) && dto.TechnicianId > 0)
            {
                resolvedTeamId = await _diagnosisService.ResolveTeamIdForTechnicianAsync(dto.TechnicianId!.Value);
            }

            if (resolvedTeamId.HasValue && resolvedTeamId.Value > 0)
            {
                dto.AssignedTeamId = resolvedTeamId.Value;

                if (!teamCache.TryGetValue(resolvedTeamId.Value, out var team))
                {
                    team = await _diagnosisService.GetTeamByIdAsync(resolvedTeamId.Value);
                    teamCache[resolvedTeamId.Value] = team;
                }

                if (team != null)
                {
                    if (string.IsNullOrWhiteSpace(dto.AssignedTeamName))
                    {
                        dto.AssignedTeamName = team.Name;
                    }

                    if (string.IsNullOrWhiteSpace(dto.AssignedTeamSpecialtyTag))
                    {
                        dto.AssignedTeamSpecialtyTag = team.SpecialtyTag;
                    }

                    if ((!dto.AssignedTeamLeaderId.HasValue || dto.AssignedTeamLeaderId.Value <= 0) &&
                        team.LeaderId.HasValue && team.LeaderId.Value > 0)
                    {
                        dto.AssignedTeamLeaderId = team.LeaderId.Value;
                    }
                }
            }

            long? leaderId = dto.AssignedTeamLeaderId;
            if ((!leaderId.HasValue || leaderId.Value <= 0) && dto.TechnicianId > 0)
            {
                leaderId = dto.TechnicianId;
                dto.AssignedTeamLeaderId = dto.TechnicianId;
            }

            if (leaderId.HasValue && leaderId.Value > 0)
            {
                var leader = await GetCachedStaffAsync(leaderId.Value, staffCache);
                if (leader != null && string.IsNullOrWhiteSpace(dto.AssignedTeamLeaderName))
                {
                    dto.AssignedTeamLeaderName = leader.FullName;
                }
            }

            if (dto.ApprovedByForemanId.HasValue && dto.ApprovedByForemanId.Value > 0)
            {
                var technicalApprover = await GetCachedStaffAsync(dto.ApprovedByForemanId.Value, staffCache);
                if (technicalApprover != null && string.IsNullOrWhiteSpace(dto.ApprovedByForemanName))
                {
                    dto.ApprovedByForemanName = technicalApprover.FullName;
                }
            }

            long? finalApproverId = dto.ApprovedByTeamLeaderId;
            if ((!finalApproverId.HasValue || finalApproverId.Value <= 0) &&
                dto.ApprovedByTeamLeaderAt.HasValue &&
                dto.UpdatedBy.HasValue &&
                dto.UpdatedBy.Value > 0)
            {
                finalApproverId = dto.UpdatedBy.Value;
                dto.ApprovedByTeamLeaderId = finalApproverId;
            }

            if (finalApproverId.HasValue && finalApproverId.Value > 0)
            {
                var finalApprover = await GetCachedStaffAsync(finalApproverId.Value, staffCache);
                if (finalApprover != null && string.IsNullOrWhiteSpace(dto.ApprovedByTeamLeaderName))
                {
                    dto.ApprovedByTeamLeaderName = finalApprover.FullName;
                }
            }

            return dto;
        }

        private async Task<Staff?> GetCachedStaffAsync(long staffId, IDictionary<long, Staff?> staffCache)
        {
            if (!staffCache.TryGetValue(staffId, out var staff))
            {
                staff = await _diagnosisService.GetStaffByIdAsync(staffId);
                staffCache[staffId] = staff;
            }

            return staff;
        }

        private void ApplyAssignmentStateToDto(Diagnosis source, DiagnosisResponseDto target)
        {
            target.DiagnosisAssignmentId = _diagnosisService.ReadDiagnosisAssignmentId(source);
            target.AssignedTeamId = _diagnosisService.ReadAssignedTeamId(source);
            target.TechnicianId = _diagnosisService.ReadTechnicianId(source);
            target.AssistantTechnicianId = _diagnosisService.ReadAssistantTechnicianId(source);
            target.AssignedBayId = _diagnosisService.ReadAssignedBayId(source);
            var teamLeaderStatus = _diagnosisService.ReadTeamLeaderApprovalStatus(source);
            var executeBranchId = source.ServiceOrder?.ExecuteBranchId;
            var isEscalated = _diagnosisService.IsTeamLeaderApprovalEscalated(source, executeBranchId);
            var isWarning = _diagnosisService.IsTeamLeaderApprovalWarning(source, executeBranchId);
            target.TeamLeaderApprovalPendingMinutes = _diagnosisService.ReadTeamLeaderApprovalPendingMinutes(source);
            target.IsTeamLeaderApprovalWarning = isWarning;
            target.IsTeamLeaderApprovalEscalated = isEscalated;
            target.TeamLeaderApprovalStatus = isEscalated &&
                                              string.Equals(teamLeaderStatus, nameof(ApprovalDecisionStatusCode.PENDING), StringComparison.OrdinalIgnoreCase)
                ? nameof(ApprovalDecisionStatusCode.ESCALATED)
                : teamLeaderStatus;
            target.IsEscalated = target.IsEscalated || isEscalated;
            target.FinalApprovalStatus = _diagnosisService.ReadFinalApprovalStatus(source);
            target.ForemanRejectReason = _diagnosisService.ReadForemanRejectReason(source);
            target.ApprovedByForemanId = _diagnosisService.ReadApprovedByForemanId(source);
            target.ApprovedByForemanAt = _diagnosisService.ReadApprovedByForemanAt(source);
            target.ApprovedByTeamLeaderId = _diagnosisService.ReadApprovedByTeamLeaderId(source);
            target.ApprovedByTeamLeaderAt = _diagnosisService.ReadApprovedByTeamLeaderAt(source);
            target.TeamLeaderNote = _diagnosisService.ReadTeamLeaderNote(source);
            target.ForemanNote = _diagnosisService.ReadForemanNote(source);
            target.CancellationRequestedAt = _diagnosisService.ReadCancellationRequestedAt(source);
            target.CancellationDecisionAt = _diagnosisService.ReadCancellationDecisionAt(source);
        }

        private async Task ApplyAssignmentVersionsToDtosAsync(IEnumerable<Diagnosis> sources, IEnumerable<DiagnosisResponseDto> targets)
        {
            var sourceList = sources?.ToList() ?? new List<Diagnosis>();
            var targetList = targets?.ToList() ?? new List<DiagnosisResponseDto>();
            if (sourceList.Count == 0 || targetList.Count == 0)
            {
                return;
            }

            var sourceById = sourceList
                .Where(x => x != null && x.Id > 0)
                .ToDictionary(x => x.Id, x => x);
            if (sourceById.Count == 0)
            {
                return;
            }

            var historyByDiagnosisId = await _diagnosisService.GetAssignmentHistoryByDiagnosisIdsAsync(sourceById.Keys);
            foreach (var dto in targetList)
            {
                if (dto == null || dto.Id <= 0)
                {
                    continue;
                }

                if (!historyByDiagnosisId.TryGetValue(dto.Id, out var historyRows) || historyRows == null)
                {
                    dto.AssignmentVersions = new List<DiagnosisAssignmentVersionDto>();
                    continue;
                }

                var currentAssignmentId = dto.DiagnosisAssignmentId;
                dto.AssignmentVersions = historyRows
                    .Select(row => new DiagnosisAssignmentVersionDto
                    {
                        DiagnosisAssignmentId = row.Id,
                        EventType = row.EventType,
                        Status = row.Status,
                        TeamLeaderApprovalStatus = row.TeamLeaderApprovalStatus,
                        FinalApprovalStatus = row.FinalApprovalStatus,
                        ForemanRejectReason = row.ForemanRejectReason,
                        AssignedAt = row.AssignedAt,
                        TechnicianAcceptedAt = row.TechnicianAcceptedAt,
                        TechnicianCompletedAt = row.TechnicianCompletedAt,
                        CreatedAt = row.CreatedAt,
                        CreatedBy = row.CreatedBy,
                        Note = row.Note,
                        IsCurrent = currentAssignmentId.HasValue && currentAssignmentId.Value > 0 && row.Id == currentAssignmentId.Value
                    })
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.DiagnosisAssignmentId)
                    .ToList();
            }
        }
    }
}
