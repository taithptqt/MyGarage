using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/diagnoses")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public partial class DiagnosisController : ControllerBase
    {
        private const string DiagnosisStatusPending = nameof(DiagnosisStatus.PENDING);
        private const string DiagnosisStatusCompleted = nameof(DiagnosisStatus.COMPLETED);

        public sealed record DiagnosisStatusPatchDto
        {
            public string? Status { get; init; }
            public string? Symptom { get; init; }
            public string? RootCause { get; init; }
            public string? Severity { get; init; }
        }

        private readonly IDiagnosisService _diagnosisService;
        private readonly IDiagnosisCrudCommandFacade _crudFacade;
        private readonly IDiagnosisQueueCommandFacade _queueFacade;
        private readonly IDiagnosisApprovalCommandFacade _approvalFacade;

        public DiagnosisController(
            IDiagnosisService diagnosisService,
            IDiagnosisCrudCommandFacade crudFacade,
            IDiagnosisQueueCommandFacade queueFacade,
            IDiagnosisApprovalCommandFacade approvalFacade)
        {
            _diagnosisService = diagnosisService;
            _crudFacade = crudFacade;
            _queueFacade = queueFacade;
            _approvalFacade = approvalFacade;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DiagnosisResponseDto>>> GetAll(
            [FromQuery] long? serviceOrderId,
            [FromQuery] bool requestOnly = false,
            [FromQuery] long? teamId = null)
        {
            var dtos = await _crudFacade.GetAllAsync(serviceOrderId, requestOnly, teamId);
            return Ok(dtos);
        }

        [HttpGet("dispatch-board")]
        public async Task<ActionResult<IEnumerable<DiagnosisDispatchBoardItemDto>>> GetDispatchBoard(
            [FromQuery] long? branchId = null,
            [FromQuery] long? teamId = null,
            [FromQuery] bool includeCompleted = false,
            [FromQuery] string? viewMode = null)
        {
            var board = await _diagnosisService.GetDispatchBoardAsync(branchId, teamId, includeCompleted, viewMode);
            return Ok(board);
        }

        [HttpGet("service-orders/{serviceOrderId:long}/dispatch-suggestions")]
        [HttpGet("~/api/v1/service-orders/{serviceOrderId:long}/dispatch-suggestions")]
        public async Task<ActionResult<DiagnosisDispatchSuggestionDto>> GetDispatchSuggestion(
            long serviceOrderId,
            [FromQuery] long teamId)
        {
            if (teamId <= 0)
            {
                return this.BadRequestProblem("Bat buoc cung cap teamId de lay goi y phan cong.");
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return this.NotFoundProblem("Không tìm thấy lệnh sửa chữa.");
            }

            var suggestion = await _diagnosisService.BuildDispatchSuggestionAsync(serviceOrderId, teamId);
            return Ok(suggestion);
        }

        [HttpGet("service-orders/{serviceOrderId:long}/intake-suggestions")]
        [HttpGet("~/api/v1/service-orders/{serviceOrderId:long}/intake-suggestions")]
        public async Task<ActionResult<DiagnosisIntakeSuggestionDto>> GetIntakeSuggestion(
            long serviceOrderId,
            [FromQuery] string? dispatchTag = null,
            [FromQuery] int? bufferMinutes = null,
            [FromQuery] string? intakePackageCode = null)
        {
            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return this.NotFoundProblem("Không tìm thấy lệnh sửa chữa.");
            }

            var suggestion = await _diagnosisService.BuildIntakeSuggestionAsync(
                serviceOrderId,
                NormalizeDispatchTag(dispatchTag),
                bufferMinutes,
                intakePackageCode);

            return Ok(suggestion);
        }

        [HttpGet("intake-packages")]
        public async Task<ActionResult<IEnumerable<DiagnosisIntakePackageOptionDto>>> GetIntakePackages()
        {
            var packages = await _diagnosisService.GetIntakePackagesAsync();
            return Ok(packages);
        }

        [HttpPost("service-orders/{serviceOrderId:long}/handover-snapshots")]
        [HttpPost("~/api/v1/service-orders/{serviceOrderId:long}/handover-snapshots")]
        public async Task<ActionResult<DiagnosisHandoverSnapshotResponseDto>> CreateHandoverSnapshot(
            long serviceOrderId,
            [FromBody] DiagnosisHandoverSnapshotCreateDto? request)
        {
            if (request == null)
            {
                return this.BadRequestProblem("Dữ liệu T2 không hợp lệ.");
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return this.NotFoundProblem("Không tìm thấy lệnh sửa chữa.");
            }

            var snapshot = await _diagnosisService.CreateHandoverSnapshotAsync(serviceOrderId, request);
            return CreatedAtAction(nameof(GetLatestHandoverSnapshot), new { serviceOrderId }, snapshot);
        }

        [HttpGet("service-orders/{serviceOrderId:long}/handover-snapshots/latest")]
        [HttpGet("~/api/v1/service-orders/{serviceOrderId:long}/handover-snapshots/latest")]
        public async Task<ActionResult<DiagnosisHandoverSnapshotResponseDto>> GetLatestHandoverSnapshot(long serviceOrderId)
        {
            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return this.NotFoundProblem("Không tìm thấy lệnh sửa chữa.");
            }

            var snapshot = await _diagnosisService.GetLatestHandoverSnapshotAsync(serviceOrderId);
            if (snapshot == null)
            {
                return this.NotFoundProblem("Chưa có snapshot T2 sau chẩn đoán.");
            }

            return Ok(snapshot);
        }

        [HttpPost("service-orders/{serviceOrderId:long}/handover-snapshots/{snapshotId:long}/customer-decisions")]
        [HttpPost("~/api/v1/service-orders/{serviceOrderId:long}/handover-snapshots/{snapshotId:long}/customer-decisions")]
        public async Task<ActionResult<DiagnosisHandoverSnapshotResponseDto>> ApplyHandoverCustomerDecision(
            long serviceOrderId,
            long snapshotId,
            [FromBody] DiagnosisHandoverDecisionCreateDto? request)
        {
            if (request == null)
            {
                return this.BadRequestProblem("Thiếu thông tin duyệt/Từ chối của khách.");
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return this.NotFoundProblem("Không tìm thấy lệnh sửa chữa.");
            }

            try
            {
                var result = await _diagnosisService.ApplyHandoverCustomerDecisionAsync(serviceOrderId, snapshotId, request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpGet("kpis/intake-conversions")]
        public async Task<ActionResult<DiagnosisIntakeKpiResponseDto>> GetIntakeConversionKpi(
            [FromQuery] long? branchId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var result = await _diagnosisService.GetIntakeConversionKpiAsync(branchId, startDate, endDate);
            return Ok(result);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<DiagnosisResponseDto>> GetById(long id)
        {
            var dto = await _crudFacade.GetByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<DiagnosisResponseDto>> Create([FromBody] DiagnosisCreateDto itemDto)
        {
            if (itemDto == null)
            {
                return this.BadRequestProblem("Dữ liệu chẩn đoán không hợp lệ.");
            }

            var responseDto = await _crudFacade.CreateAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<DiagnosisResponseDto>> Update(long id, [FromBody] DiagnosisUpdateDto itemDto)
        {
            var responseDto = await _crudFacade.UpdateAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }

            return Ok(responseDto);
        }
    }
}
