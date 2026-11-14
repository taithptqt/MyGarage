// Mục đích: Cung cấp API CRUD và các thao tác vòng đời cho bản ghi voucher chính.
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/vouchers")]
    [Route("api/v1/vouchers")]
    [ApiController]
    [Authorize]
    public class VoucherController : ControllerBase
    {
        private const string VoucherStatusActive = nameof(VoucherStatusCode.ACTIVE);
        private const string VoucherStatusPaused = nameof(VoucherStatusCode.PAUSED);
        private const string VoucherStatusRevoked = nameof(VoucherStatusCode.REVOKED);
        private const string VoucherStatusArchived = nameof(VoucherStatusCode.ARCHIVED);
        private const string VoucherStatusExpired = nameof(VoucherStatusCode.EXPIRED);
        private const string VoucherStatusScheduled = nameof(VoucherStatusCode.SCHEDULED);
        private const string VoucherStatusExhausted = nameof(VoucherStatusCode.EXHAUSTED);

        public sealed record VoucherStatusPatchDto
        {
            public string? Status { get; init; }
        }

        private readonly IVoucherService _voucherService;
        private readonly IMapper _mapper;

        public VoucherController(IVoucherService voucherService, IMapper mapper)
        {
            _voucherService = voucherService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<VoucherResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _voucherService.GetPageByIdAsync(normalizedPage, normalizedSize);
            var dtos = _mapper.Map<List<VoucherResponseDto>>(paged);
            return Ok(PagedEnvelope<VoucherResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<VoucherResponseDto>> GetById(long id)
        {
            var item = await _voucherService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<VoucherResponseDto>(item);
            return Ok(dto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        public async Task<ActionResult<VoucherResponseDto>> Create([FromBody] VoucherCreateDto itemDto)
        {
            var entity = _mapper.Map<Voucher>(itemDto);
            entity.VoucherName ??= entity.Code;
            entity.Status ??= ResolveStatus(entity);
            var createdItem = await _voucherService.AddAsync(entity);
            var responseDto = _mapper.Map<VoucherResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPut("{id}")]
        public async Task<ActionResult<VoucherResponseDto>> Update(long id, [FromBody] VoucherUpdateDto itemDto)
        {
            var existingItem = await _voucherService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _voucherService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<VoucherResponseDto>(updatedItem);
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
                _ => BadRequest(new { message = "Status must be ACTIVE, PAUSED, or REVOKED." }),
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
            var existingItem = await _voucherService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            existingItem.Status = status;
            existingItem.IsActive = isActive;
            existingItem.UpdatedAt = VietnamTime.Now;

            var updatedItem = await _voucherService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<VoucherResponseDto>(updatedItem));
        }

        private static string ResolveStatus(Voucher voucher)
        {
            var now = VietnamTime.Now;

            if (voucher.IsDeleted == true)
            {
                return VoucherStatusArchived;
            }

            if (voucher.IsActive == false)
            {
                return VoucherStatusPaused;
            }

            if (voucher.EndDate < now)
            {
                return VoucherStatusExpired;
            }

            if (voucher.StartDate > now)
            {
                return VoucherStatusScheduled;
            }

            if (voucher.UsageLimit.HasValue && voucher.UsageLimit > 0 && voucher.UsedCount.HasValue && voucher.UsedCount >= voucher.UsageLimit)
            {
                return VoucherStatusExhausted;
            }

            return VoucherStatusActive;
        }
    }
}








