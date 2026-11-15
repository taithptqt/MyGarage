using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Security;

namespace MyGarage.Controllers
{
    [Route("api/v1/vehicle-condition-logs")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class VehicleConditionLogController : ControllerBase
    {
        public sealed record ConditionRequestCreateDto
        {
            public long ServiceOrderId { get; init; }
            public string? RequestType { get; init; }
        }

        public sealed record ConditionRequestAssignmentDto
        {
            public string? RequestType { get; init; }
            public long TechnicianId { get; init; }
        }

        private readonly IVehicleConditionLogCrudService _vehicleConditionLogCrudService;
        private readonly IVehicleConditionLogReadService _vehicleConditionLogReadService;
        private readonly IVehicleConditionLogCommandFacade _commandFacade;
        private readonly IFileSecurityScanner _fileSecurityScanner;
        private readonly ILogger<VehicleConditionLogController> _logger;

        public VehicleConditionLogController(
            IVehicleConditionLogCrudService vehicleConditionLogCrudService,
            IVehicleConditionLogReadService vehicleConditionLogReadService,
            IVehicleConditionLogCommandFacade commandFacade,
            IFileSecurityScanner fileSecurityScanner,
            ILogger<VehicleConditionLogController> logger)
        {
            _vehicleConditionLogCrudService = vehicleConditionLogCrudService;
            _vehicleConditionLogReadService = vehicleConditionLogReadService;
            _commandFacade = commandFacade;
            _fileSecurityScanner = fileSecurityScanner;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleConditionLogResponseDto>>> GetAll([FromQuery] long? serviceOrderId = null)
        {
            var dtos = serviceOrderId.HasValue
                ? await _vehicleConditionLogReadService.GetByOrderDtosAsync(serviceOrderId.Value)
                : await _vehicleConditionLogCrudService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> GetById(long id)
        {
            var dto = await _vehicleConditionLogCrudService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpGet("condition-jobs")]
        public async Task<ActionResult<IEnumerable<VehicleConditionLogResponseDto>>> GetRequests([FromQuery] string? requestType = null)
        {
            var type = (requestType ?? "CHECK_IN").Trim().ToUpperInvariant();
            if (type != "CHECK_IN" && type != "CHECK_OUT")
            {
                return this.BadRequestProblem("requestType must be CHECK_IN or CHECK_OUT.");
            }

            var dtos = await _vehicleConditionLogReadService.GetActiveRequestDtosAsync(type);
            return Ok(dtos);
        }

        [HttpPost("condition-jobs")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> CreateRequest([FromBody] ConditionRequestCreateDto request)
        {
            var result = await _commandFacade.CreateRequestAsync(request.ServiceOrderId, request.RequestType);
            return ToActionResult(result);
        }

        [HttpPatch("condition-jobs/{serviceOrderId:long}")]
        public Task<ActionResult<VehicleConditionLogResponseDto>> PatchRequestAssignment(
            long serviceOrderId,
            [FromBody] ConditionRequestAssignmentDto request)
        {
            _ = serviceOrderId;
            var type = (request.RequestType ?? string.Empty).Trim().ToUpperInvariant();
            if (type == "CHECK_IN" || type == "CHECK_OUT")
            {
                return Task.FromResult<ActionResult<VehicleConditionLogResponseDto>>(
                    this.BadRequestProblem("Yêu cầu CHECK_IN/CHECK_OUT do lễ tân xử lý trực tiếp, không phân công cho kỹ thuật viên."));
            }

            return Task.FromResult<ActionResult<VehicleConditionLogResponseDto>>(
                this.BadRequestProblem("requestType must be CHECK_IN or CHECK_OUT."));
        }

        [HttpPost]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> Create([FromBody] VehicleConditionLogCreateDto itemDto)
        {
            var result = await _commandFacade.CreateAsync(itemDto, HttpContext.RequestAborted);
            if (result.StatusCode == 201 && result.Data != null)
            {
                return CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result.Data);
            }

            return ToActionResult(result);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> Update(long id, [FromBody] VehicleConditionLogUpdateDto itemDto)
        {
            var result = await _commandFacade.UpdateAsync(id, itemDto, HttpContext.RequestAborted);
            return ToActionResult(result);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _vehicleConditionLogCrudService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpPost("attachments")]
        public Task<ActionResult<object>> UploadAttachment(IFormFile file)
        {
            return UploadMediaInternalAsync(
                file,
                folderName: "vehicle-condition",
                emptyFileMessage: "Dữ liệu ảnh không hợp lệ.",
                allowVideo: true,
                invalidTypeMessage: "Chỉ hỗ trợ tệp ảnh hoặc video.",
                uploadErrorMessage: "Không thể tải tệp lên máy chủ.");
        }

        [HttpPost("signatures")]
        public Task<ActionResult<object>> CreateSignature(IFormFile file)
        {
            return UploadMediaInternalAsync(
                file,
                folderName: "signatures",
                emptyFileMessage: "Dữ liệu chữ ký không hợp lệ.",
                allowVideo: false,
                invalidTypeMessage: "Chỉ hỗ trợ tệp ảnh cho chữ ký.",
                uploadErrorMessage: "Không thể tải chữ ký lên máy chủ.");
        }

        private async Task<ActionResult<object>> UploadMediaInternalAsync(
            IFormFile? file,
            string folderName,
            string emptyFileMessage,
            bool allowVideo,
            string invalidTypeMessage,
            string uploadErrorMessage)
        {
            if (file == null || file.Length == 0)
            {
                return this.BadRequestProblem(emptyFileMessage);
            }

            var isImage = !string.IsNullOrWhiteSpace(file.ContentType) &&
                          file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            var isVideo = allowVideo &&
                          !string.IsNullOrWhiteSpace(file.ContentType) &&
                          file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

            if (!isImage && !isVideo)
            {
                return this.BadRequestProblem(invalidTypeMessage);
            }

            try
            {
                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? ".mp4" : ".png";
                }

                var scan = await _fileSecurityScanner.ScanFormFileAsync(
                    file,
                    fileNameOverride: string.IsNullOrWhiteSpace(Path.GetExtension(file.FileName)) ? $"upload{extension}" : file.FileName,
                    maxBytes: 20 * 1024 * 1024,
                    cancellationToken: HttpContext.RequestAborted);
                if (!scan.IsClean)
                {
                    return this.BadRequestProblem(scan.ErrorMessage ?? "Tệp không hợp lệ.");
                }

                var result = await _commandFacade.UploadMediaAsync(
                    file.FileName,
                    file.ContentType,
                    scan.Bytes,
                    folderName,
                    HttpContext.RequestAborted);
                return ToActionResult(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload vehicle condition media to folder {FolderName}.", folderName);
                return this.BadRequestProblem(uploadErrorMessage);
            }
        }

        private ActionResult<T> ToActionResult<T>(VehicleConditionLogCommandResult<T> result)
        {
            return result.StatusCode switch
            {
                200 => Ok(result.Data),
                404 => result.ErrorPayload == null ? NotFound() : NotFound(result.ErrorPayload),
                400 => BadRequest(result.ErrorPayload),
                _ => StatusCode(result.StatusCode, result.ErrorPayload ?? result.Data)
            };
        }
    }
}
