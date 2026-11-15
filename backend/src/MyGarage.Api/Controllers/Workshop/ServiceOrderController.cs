using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Authorization;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Security;

namespace MyGarage.Controllers
{
    [Route("api/v1/service-orders")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public partial class ServiceOrderController : ControllerBase
    {
        private const long MaxInsuranceAssetBytes = 10 * 1024 * 1024;

        private readonly IServiceOrderService _serviceOrderService;
        private readonly IFileSecurityScanner _fileSecurityScanner;
        private readonly IServiceOrderAuthorizationService _serviceOrderAuthService;
        private readonly IServiceOrderCommandFacade _commandFacade;

        public ServiceOrderController(
            IServiceOrderService serviceOrderService,
            IFileSecurityScanner fileSecurityScanner,
            IServiceOrderAuthorizationService serviceOrderAuthService,
            IServiceOrderCommandFacade commandFacade)
        {
            _serviceOrderService = serviceOrderService;
            _fileSecurityScanner = fileSecurityScanner;
            _serviceOrderAuthService = serviceOrderAuthService;
            _commandFacade = commandFacade;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<ServiceOrderResponseDto>>> GetAll(
            [FromQuery] long? branchId = null,
            [FromQuery] string? status = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            return Ok(await _commandFacade.GetPagedAsync(scopedBranchId, status, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderResponseDto>> GetById(long id)
        {
            if (!await _serviceOrderAuthService.CanAccessServiceOrderAsync(id, this.ToUserActorContext()))
            {
                return Forbid();
            }

            var dto = await _commandFacade.GetByIdAsync(id);
            return dto == null ? NotFound() : Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderResponseDto>> Create([FromBody] ServiceOrderCreateDto itemDto)
        {
            var result = await _commandFacade.CreateAsync(itemDto, this.GetCurrentUserId());
            if (result.StatusCode == 201 && result.Data != null)
            {
                return CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result.Data);
            }

            return ToActionResult(result);
        }

        [HttpPost("batches")]
        public async Task<ActionResult<ServiceOrderBatchCreateResponseDto>> CreateBatch([FromBody] ServiceOrderBatchCreateRequestDto dto)
        {
            var result = await _commandFacade.CreateBatchAsync(dto, this.GetCurrentUserId());
            return ToActionResult(result);
        }

        [HttpPut("{id}/workshop")]
        public async Task<ActionResult<ServiceOrderResponseDto>> UpdateWorkshop(long id, [FromBody] WorkshopServiceOrderEditRequestDto dto)
        {
            if (!await _serviceOrderAuthService.CanMutateWorkshopAsync(id, this.ToUserActorContext()))
            {
                return Forbid();
            }

            var result = await _commandFacade.UpdateWorkshopAsync(id, dto, this.GetCurrentUserId());
            return ToActionResult(result);
        }

        [HttpGet("{id}/handover-readiness")]
        public async Task<ActionResult<HandoverReadinessResponseDto>> GetHandoverReadiness(long id)
        {
            if (!await _serviceOrderAuthService.CanAccessServiceOrderAsync(id, this.ToUserActorContext()))
            {
                return Forbid();
            }

            var readiness = await _serviceOrderService.GetHandoverReadinessAsync(id);
            return readiness == null ? NotFound() : Ok(readiness);
        }

        [HttpPost("{id}/handovers")]
        public async Task<IActionResult> ProcessHandover(long id, [FromBody] HandoverRequestDto request)
        {
            if (!await _serviceOrderAuthService.CanPerformHandoverAsync(id, this.ToUserActorContext()))
            {
                return Forbid();
            }

            try
            {
                var result = await _serviceOrderService.ProcessHandoverAsync(id, request);
                return !result ? NotFound() : Ok(new { message = "Vehicle handover completed successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderResponseDto>> Update(long id, [FromBody] ServiceOrderUpdateDto itemDto)
        {
            if (!await _serviceOrderAuthService.CanMutateWorkshopAsync(id, this.ToUserActorContext()))
            {
                return Forbid();
            }

            var result = await _commandFacade.UpdateAsync(id, itemDto, this.GetCurrentUserId());
            return ToActionResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!await _serviceOrderAuthService.CanMutateWorkshopAsync(id, this.ToUserActorContext()))
            {
                return Forbid();
            }

            var result = await _commandFacade.DeleteAsync(id);
            return ToActionResult(result);
        }

        [Authorize]
        [HttpPost("insurance-assets")]
        public async Task<ActionResult<object>> UploadInsuranceAsset(IFormFile file, [FromForm] string? assetType)
        {
            if (file == null || file.Length == 0)
            {
                return this.BadRequestProblem("Dữ liệu ảnh bảo hiểm không hợp lệ.");
            }

            if (file.Length > MaxInsuranceAssetBytes)
            {
                return this.BadRequestProblem("Ảnh bảo hiểm vượt quá dung lượng 10MB.");
            }

            if (string.IsNullOrWhiteSpace(file.ContentType) ||
                !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return this.BadRequestProblem("Chỉ hỗ trợ tệp ảnh cho hồ sơ bảo hiểm.");
            }

            var scan = await _fileSecurityScanner.ScanFormFileAsync(
                file,
                maxBytes: MaxInsuranceAssetBytes,
                cancellationToken: HttpContext.RequestAborted);
            if (!scan.IsClean)
            {
                return this.BadRequestProblem(scan.ErrorMessage ?? "Tệp không hợp lệ.");
            }

            var result = await _commandFacade.UploadInsuranceAssetAsync(
                file.FileName,
                file.ContentType,
                scan.Bytes,
                assetType,
                HttpContext.RequestAborted);
            return ToActionResult(result);
        }

        [HttpGet("{id}/edit-capabilities")]
        public async Task<ActionResult<ServiceOrderEditCapabilityResponseDto>> GetEditCapabilities(long id)
        {
            if (!await _serviceOrderAuthService.CanAccessServiceOrderAsync(id, this.ToUserActorContext()))
            {
                return Forbid();
            }

            var capability = await _serviceOrderService.GetEditCapabilityAsync(id);
            return capability == null ? NotFound() : Ok(capability);
        }

        [Authorize]
        [HttpGet("{id}/edit-requests")]
        public async Task<IActionResult> GetEditRequests(long id, [FromQuery] string? status = null)
        {
            if (!await _serviceOrderAuthService.CanAccessServiceOrderAsync(id, this.ToUserActorContext()))
            {
                return Forbid();
            }

            return Ok(await _commandFacade.GetEditRequestsAsync(id, status));
        }

        [Authorize]
        [HttpPost("{id}/edit-requests")]
        public async Task<IActionResult> RequestEdit(long id, [FromBody] EditRequestCreateDto? dto)
        {
            if (!await _serviceOrderAuthService.CanAccessServiceOrderAsync(id, this.ToUserActorContext()))
            {
                return Forbid();
            }

            var result = await _commandFacade.RequestEditAsync(id, this.GetCurrentUserId(), dto);
            return ToActionResult(result);
        }

        [Authorize]
        [HttpPatch("{id}/edit-requests/{requestId:long}")]
        public async Task<IActionResult> DecideEditRequest(long id, long requestId, [FromBody] EditRequestDecisionDto? dto)
        {
            var result = await _commandFacade.DecideEditRequestAsync(
                id,
                requestId,
                this.GetCurrentUserId(),
                this.GetCurrentRoleCode(),
                this.GetCurrentRoleName(),
                dto);
            return ToActionResult(result);
        }

        private IActionResult ToActionResult(ServiceOrderCommandResult result) =>
            result.StatusCode switch
            {
                200 => result.Payload == null ? Ok() : Ok(result.Payload),
                204 => NoContent(),
                400 => BadRequest(result.Payload),
                401 => Unauthorized(),
                403 => Forbid(),
                404 => result.Payload == null ? NotFound() : NotFound(result.Payload),
                _ => StatusCode(result.StatusCode, result.Payload)
            };

        private ActionResult<T> ToActionResult<T>(ServiceOrderCommandResult<T> result) =>
            result.StatusCode switch
            {
                200 => Ok(result.Data),
                400 => BadRequest(result.ErrorPayload),
                401 => Unauthorized(),
                403 => Forbid(),
                404 => result.ErrorPayload == null ? NotFound() : NotFound(result.ErrorPayload),
                _ => StatusCode(result.StatusCode, result.ErrorPayload)
            };
    }
}
