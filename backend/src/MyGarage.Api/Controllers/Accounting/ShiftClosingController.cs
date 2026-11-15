using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;

namespace MyGarage.Controllers
{
    [Route("api/v1/shift-closings")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ShiftClosingController : ControllerBase
    {
        private readonly IShiftClosingService _shiftClosingService;
        private readonly IShiftClosingDataService _dataService;

        public ShiftClosingController(IShiftClosingService shiftClosingService, IShiftClosingDataService dataService)
        {
            _shiftClosingService = shiftClosingService;
            _dataService = dataService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<ShiftClosingResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _shiftClosingService.GetPageByIdAsync(normalizedPage, normalizedSize);
            var dtos = _shiftClosingService.MapToDtos(paged);
            return Ok(PagedEnvelope<ShiftClosingResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ShiftClosingResponseDto>> GetById(long id)
        {
            var dto = await _shiftClosingService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ShiftClosingResponseDto>> Create([FromBody] ShiftClosingCreateDto itemDto)
        {
            var responseDto = await _shiftClosingService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ShiftClosingResponseDto>> Update(long id, [FromBody] ShiftClosingUpdateDto itemDto)
        {
            var responseDto = await _shiftClosingService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _shiftClosingService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpPost("{id:long}/reconciliations")]
        public async Task<IActionResult> ReconcileShift(long id, [FromBody] ShiftReconciliationRequestDto? request)
        {
            var result = await _dataService.ReconcileShiftAsync(
                id,
                request?.ActualBalance,
                request?.TotalCashOut,
                request?.ManagerId,
                request?.Note,
                this.GetCurrentUserId(),
                HttpContext.RequestAborted);
            if (!result.Found)
            {
                return this.NotFoundProblem("Không tìm thấy ca thu ngân.");
            }

            if (result.AlreadyClosed)
            {
                return this.BadRequestProblem("Ca đã đóng, không thể đối soát lại.");
            }

            return Ok(new
            {
                shiftId = result.ShiftId,
                Status = result.Status,
                OpeningBalance = result.OpeningBalance,
                TotalCashIn = result.TotalCashIn,
                transferIn = result.TransferIn,
                onlineIn = result.OnlineIn,
                TotalCashOut = result.TotalCashOut,
                ExpectedBalance = result.ExpectedBalance,
                ActualBalance = result.ActualBalance,
                Discrepancy = result.Discrepancy,
                ClosedAt = result.ClosedAt
            });
        }

        public sealed class ShiftReconciliationRequestDto
        {
            public decimal? ActualBalance { get; set; }
            public decimal? TotalCashOut { get; set; }
            public long? ManagerId { get; set; }
            public string? Note { get; set; }
        }
    }
}






