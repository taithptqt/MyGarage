// Mục đích: Cung cấp API CRUD và các thao tác vòng đời cho bản ghi voucher chính.
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/vouchers")]
[Route("api/[controller]")]
    [ApiController]
    public class VoucherController : ControllerBase
    {
        private readonly IVoucherService _voucherService;
        private readonly IMapper _mapper;

        public VoucherController(IVoucherService voucherService, IMapper mapper)
        {
            _voucherService = voucherService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VoucherResponseDto>>> GetAll()
        {
            var items = await _voucherService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<VoucherResponseDto>>(items);
            return Ok(dtos);
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

        [HttpPost("{id}/activate")]
        public async Task<ActionResult<VoucherResponseDto>> Activate(long id)
        {
            return await UpdateStatusAsync(id, "ACTIVE", true);
        }

        [HttpPost("{id}/pause")]
        public async Task<ActionResult<VoucherResponseDto>> Pause(long id)
        {
            return await UpdateStatusAsync(id, "PAUSED", false);
        }

        [HttpPost("{id}/revoke")]
        public async Task<ActionResult<VoucherResponseDto>> Revoke(long id)
        {
            return await UpdateStatusAsync(id, "REVOKED", false);
        }

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
            existingItem.UpdatedAt = DateTime.UtcNow;

            var updatedItem = await _voucherService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<VoucherResponseDto>(updatedItem));
        }

        private static string ResolveStatus(Voucher voucher)
        {
            var now = DateTime.UtcNow;

            if (voucher.IsDeleted == true)
            {
                return "ARCHIVED";
            }

            if (voucher.IsActive == false)
            {
                return "PAUSED";
            }

            if (voucher.EndDate < now)
            {
                return "EXPIRED";
            }

            if (voucher.StartDate > now)
            {
                return "SCHEDULED";
            }

            if (voucher.UsageLimit.HasValue && voucher.UsageLimit > 0 && voucher.UsedCount.HasValue && voucher.UsedCount >= voucher.UsageLimit)
            {
                return "EXHAUSTED";
            }

            return "ACTIVE";
        }
    }
}





