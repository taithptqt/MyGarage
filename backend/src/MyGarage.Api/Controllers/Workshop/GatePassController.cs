using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Extensions;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.GatePasses;

namespace MyGarage.Controllers
{
    [Route("api/gate-passes")]
    [Route("api/v1/gate-passes")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class GatePassController : ControllerBase
    {
        private readonly IGatePassService _gatePassService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IMapper _mapper;
        private readonly GatePassListQuery _listQuery;

        public GatePassController(
            IGatePassService gatePassService,
            IServiceOrderService serviceOrderService,
            IMapper mapper,
            GatePassListQuery listQuery)
        {
            _gatePassService = gatePassService;
            _serviceOrderService = serviceOrderService;
            _mapper = mapper;
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
                Id = item.Id, ServiceOrderId = item.ServiceOrderId, ClearanceType = item.ClearanceType,
                QrCode = item.QrCode, IssuedBy = item.IssuedBy, ApprovedBy = item.ApprovedBy,
                ResolutionNotes = item.ResolutionNotes, OldPartsPackingListUrl = item.OldPartsPackingListUrl,
                IsOldPartsChecked = item.IsOldPartsChecked, Status = item.Status, ScannedBy = item.ScannedBy,
                SecurityCheckedAt = item.SecurityCheckedAt, PassedAt = item.PassedAt
            });
            return Ok(PagedEnvelope<GatePassResponseDto>.Create(mapped, result.Total, current, size));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GatePassResponseDto>> GetById(long id)
        {
            var item = await _gatePassService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<GatePassResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<GatePassResponseDto>> Create([FromBody] GatePassCreateDto itemDto)
        {
            try
            {
                if (itemDto == null || itemDto.ServiceOrderId <= 0)
                {
                    return BadRequest(new { message = "Thiếu ServiceOrderId hợp lệ để tạo phiếu xuất cổng." });
                }

                var lockError = await ValidateGatePassMutationAsync(itemDto.ServiceOrderId, existingGatePass: null);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                var entity = _mapper.Map<GatePass>(itemDto);
                var createdItem = await _gatePassService.AddAsync(entity);
                var responseDto = _mapper.Map<GatePassResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<GatePassResponseDto>> Update(long id, [FromBody] GatePassUpdateDto itemDto)
        {
            try
            {
                var existingItem = await _gatePassService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var targetServiceOrderId = itemDto?.ServiceOrderId > 0
                    ? itemDto.ServiceOrderId
                    : existingItem.ServiceOrderId;
                var lockError = await ValidateGatePassMutationAsync(targetServiceOrderId, existingItem);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                _mapper.Map(itemDto, existingItem);
                var updatedItem = await _gatePassService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }
                var responseDto = _mapper.Map<GatePassResponseDto>(updatedItem);
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var existingItem = await _gatePassService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var lockError = await ValidateGatePassMutationAsync(existingItem.ServiceOrderId, existingItem);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                var result = await _gatePassService.DeleteAsync(id);
                if (!result)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<string?> ValidateGatePassMutationAsync(long serviceOrderId, GatePass? existingGatePass)
        {
            if (existingGatePass != null && IsGatePassFinalized(existingGatePass))
            {
                return "Không thể chỉnh sửa phiếu xuất cổng vì xe đã được xác nhận ra cổng.";
            }

            if (serviceOrderId <= 0)
            {
                return "Thiếu ServiceOrderId hợp lệ để xử lý phiếu xuất cổng.";
            }

            var capability = await _serviceOrderService.GetEditCapabilityAsync(serviceOrderId);
            if (capability == null)
            {
                return "Không tìm thấy service order để xử lý phiếu xuất cổng.";
            }

            if (capability.IsClosedLocked)
            {
                return "Không thể chỉnh sửa phiếu xuất cổng vì service order đã đóng.";
            }

            return null;
        }

        private static bool IsGatePassFinalized(GatePass gatePass)
        {
            if (gatePass.PassedAt.HasValue)
            {
                return true;
            }

            return string.Equals(
                NormalizeStatus(gatePass.Status),
                nameof(GatePassStatusCode.SCANNED),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeStatus(string? status)
        {
            return string.IsNullOrWhiteSpace(status)
                ? string.Empty
                : status.Trim().ToUpperInvariant().Replace("-", "_").Replace(" ", "_");
        }
    }
}






