using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/diagnosis-recommendations")]
    [Route("api/v1/diagnose-recommendations")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class DiagnosisRecommendationController : ControllerBase
    {
        private readonly IDiagnosisRecommendationCommandService _commandService;

        public DiagnosisRecommendationController(IDiagnosisRecommendationCommandService commandService)
        {
            _commandService = commandService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<DiagnosisRecommendationResponseDto>>> GetAll(
            [FromQuery] long? diagnosisId,
            [FromQuery] long? diagnosisAssignmentId,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var envelope = await _commandService.GetAllAsync(
                diagnosisId,
                diagnosisAssignmentId,
                page,
                pageSize,
                HttpContext.RequestAborted);
            return Ok(envelope);
        }

        [HttpGet("pending-supplementary")]
        public async Task<ActionResult<IEnumerable<PendingSupplementaryProposalResponseDto>>> GetPendingSupplementary(
            [FromQuery] long? serviceOrderId = null,
            [FromQuery] long? branchId = null)
        {
            var pendingGroups = await _commandService.GetPendingSupplementaryAsync(
                serviceOrderId,
                branchId,
                HttpContext.RequestAborted);
            return Ok(pendingGroups);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<DiagnosisRecommendationResponseDto>> GetById(long id)
        {
            var dto = await _commandService.GetByIdAsync(id, HttpContext.RequestAborted);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<DiagnosisRecommendationResponseDto>> Create([FromBody] DiagnosisRecommendationCreateDto itemDto)
        {
            var result = await _commandService.CreateAsync(itemDto, HttpContext.RequestAborted);
            return result.Status switch
            {
                DiagnosisRecommendationCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Dữ liệu kiến nghị kỹ thuật không hợp lệ."),
                _ => CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data!)
            };
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<DiagnosisRecommendationResponseDto>> Update(long id, [FromBody] DiagnosisRecommendationUpdateDto itemDto)
        {
            var result = await _commandService.UpdateAsync(id, itemDto, HttpContext.RequestAborted);
            return result.Status switch
            {
                DiagnosisRecommendationCommandStatus.NotFound => NotFound(),
                DiagnosisRecommendationCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Dữ liệu cập nhật không hợp lệ."),
                _ => Ok(result.Data!)
            };
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _commandService.DeleteAsync(id, HttpContext.RequestAborted);
            return result.Status switch
            {
                DiagnosisRecommendationCommandStatus.NotFound => NotFound(),
                DiagnosisRecommendationCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Không thể xóa kiến nghị kỹ thuật."),
                _ => NoContent()
            };
        }
    }
}
