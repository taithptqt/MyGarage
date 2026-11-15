// Mục đích: Cung cấp API CRUD và các thao tác vòng đời cho bản ghi voucher chính.
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/v1/vouchers")]
    [ApiController]
    [Authorize]
    public class VoucherController : ControllerBase
    {
        private const string VoucherStatusActive = nameof(VoucherStatusCode.ACTIVE);
        private const string VoucherStatusPaused = nameof(VoucherStatusCode.PAUSED);
        private const string VoucherStatusRevoked = nameof(VoucherStatusCode.REVOKED);

        public sealed record VoucherStatusPatchDto
        {
            public string? Status { get; init; }
        }

        private readonly IVoucherService _voucherService;

        public VoucherController(IVoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<VoucherResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (dtos, total) = await _voucherService.GetPageDtosByIdAsync(normalizedPage, normalizedSize);
            return Ok(PagedEnvelope<VoucherResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<VoucherResponseDto>> GetById(long id)
        {
            var dto = await _voucherService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        public async Task<ActionResult<VoucherResponseDto>> Create([FromBody] VoucherCreateDto itemDto)
        {
            var responseDto = await _voucherService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPut("{id}")]
        public async Task<ActionResult<VoucherResponseDto>> Update(long id, [FromBody] VoucherUpdateDto itemDto)
        {
            var responseDto = await _voucherService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPatch("{id}")]
        public async Task<ActionResult<VoucherResponseDto>> PatchStatus(long id, [FromBody] VoucherStatusPatchDto dto)
        {
            var status = (dto?.Status ?? string.Empty).Trim().ToUpperInvariant();
            return status switch
            {
                VoucherStatusActive => await UpdateStatusAsync(id, VoucherStatusActive, true),
                VoucherStatusPaused => await UpdateStatusAsync(id, VoucherStatusPaused, false),
                VoucherStatusRevoked => await UpdateStatusAsync(id, VoucherStatusRevoked, false),
                _ => this.BadRequestProblem("Status must be ACTIVE, PAUSED, or REVOKED."),
            };
        }

        [Authorize(Roles = "Staff")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _voucherService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        private async Task<ActionResult<VoucherResponseDto>> UpdateStatusAsync(long id, string status, bool isActive)
        {
            var updatedDto = await _voucherService.UpdateStatusDtoAsync(id, status, isActive);
            if (updatedDto == null)
            {
                return NotFound();
            }

            return Ok(updatedDto);
        }
    }
}








