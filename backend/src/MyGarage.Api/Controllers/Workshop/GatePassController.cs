using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.GatePasses;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/gate-passes")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class GatePassController : ControllerBase
    {
        private readonly IGatePassCommandService _gatePassCommandService;
        private readonly GatePassListQuery _listQuery;

        public GatePassController(
            IGatePassCommandService gatePassCommandService,
            GatePassListQuery listQuery)
        {
            _gatePassCommandService = gatePassCommandService;
            _listQuery = listQuery;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<GatePassResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            CancellationToken cancellationToken = default)
        {
            var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize);
            var result = await _listQuery.ExecuteAsync(current, size, cancellationToken);
            var mapped = result.Items.Select(item => new GatePassResponseDto
            {
                Id = item.Id,
                ServiceOrderId = item.ServiceOrderId,
                ClearanceType = item.ClearanceType,
                QrCode = item.QrCode,
                IssuedBy = item.IssuedBy,
                ApprovedBy = item.ApprovedBy,
                ResolutionNotes = item.ResolutionNotes,
                OldPartsPackingListUrl = item.OldPartsPackingListUrl,
                IsOldPartsChecked = item.IsOldPartsChecked,
                Status = item.Status,
                ScannedBy = item.ScannedBy,
                SecurityCheckedAt = item.SecurityCheckedAt,
                PassedAt = item.PassedAt
            });
            return Ok(PagedEnvelope<GatePassResponseDto>.Create(mapped, result.Total, current, size));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GatePassResponseDto>> GetById(long id)
        {
            var dto = await _gatePassCommandService.GetByIdAsync(id, HttpContext.RequestAborted);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<GatePassResponseDto>> Create([FromBody] GatePassCreateDto itemDto)
        {
            var result = await _gatePassCommandService.CreateGatePassAsync(itemDto, HttpContext.RequestAborted);
            return result.Status switch
            {
                GatePassCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Dữ liệu phiếu xuất cổng không hợp lệ."),
                _ => CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data!)
            };
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<GatePassResponseDto>> Update(long id, [FromBody] GatePassUpdateDto itemDto)
        {
            var result = await _gatePassCommandService.UpdateGatePassAsync(id, itemDto, HttpContext.RequestAborted);
            return result.Status switch
            {
                GatePassCommandStatus.NotFound => NotFound(),
                GatePassCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Không thể cập nhật phiếu xuất cổng."),
                _ => Ok(result.Data!)
            };
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _gatePassCommandService.DeleteGatePassAsync(id, HttpContext.RequestAborted);
            return result.Status switch
            {
                GatePassCommandStatus.NotFound => NotFound(),
                GatePassCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Không thể xóa phiếu xuất cổng."),
                _ => NoContent()
            };
        }
    }
}
