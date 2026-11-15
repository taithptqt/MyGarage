using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Common;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/shift-closings")]
    [Route("api/v1/shift-closings")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ShiftClosingController : ControllerBase
    {
        private readonly IShiftClosingService _shiftClosingService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _context;

        public ShiftClosingController(IShiftClosingService shiftClosingService, IMapper mapper, IGarageContext context)
        {
            _shiftClosingService = shiftClosingService;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<ShiftClosingResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _shiftClosingService.GetPageByIdAsync(normalizedPage, normalizedSize);
            var dtos = _mapper.Map<List<ShiftClosingResponseDto>>(paged);
            return Ok(PagedEnvelope<ShiftClosingResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ShiftClosingResponseDto>> GetById(long id)
        {
            var item = await _shiftClosingService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ShiftClosingResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ShiftClosingResponseDto>> Create([FromBody] ShiftClosingCreateDto itemDto)
        {
            var entity = _mapper.Map<ShiftClosing>(itemDto);
            var createdItem = await _shiftClosingService.AddAsync(entity);
            var responseDto = _mapper.Map<ShiftClosingResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ShiftClosingResponseDto>> Update(long id, [FromBody] ShiftClosingUpdateDto itemDto)
        {
            var existingItem = await _shiftClosingService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _shiftClosingService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<ShiftClosingResponseDto>(updatedItem);
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

        [HttpPost("{id:long}/reconcile")]
        public async Task<IActionResult> ReconcileShift(long id, [FromBody] ShiftReconciliationRequestDto? request)
        {
            var shift = await _context.ShiftClosings
                .FirstOrDefaultAsync(x => x.Id == id);
            if (shift == null)
            {
                return NotFound(new { message = "Không tìm thấy ca thu ngân." });
            }

            if (!string.IsNullOrWhiteSpace(shift.Status) &&
                string.Equals(shift.Status, "CLOSED", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Ca đã đóng, không thể đối soát lại." });
            }

            var paymentRows = await (
                from payment in _context.Payments.AsNoTracking()
                join method in _context.PaymentMethods.AsNoTracking() on payment.MethodId equals method.Id
                where payment.ShiftClosingId == id &&
                      string.Equals(payment.Status ?? string.Empty, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                select new
                {
                    payment.Amount,
                    MethodCode = (method.MethodCode ?? string.Empty).Trim().ToUpperInvariant(),
                    MethodName = (method.MethodName ?? string.Empty).Trim().ToUpperInvariant()
                }
            ).ToListAsync();

            decimal cashIn = 0m;
            decimal transferIn = 0m;
            decimal onlineIn = 0m;

            foreach (var row in paymentRows)
            {
                if (row.MethodCode.Contains("CASH", StringComparison.OrdinalIgnoreCase) ||
                    row.MethodName.Contains("TIEN MAT", StringComparison.OrdinalIgnoreCase))
                {
                    cashIn += row.Amount;
                    continue;
                }

                if (row.MethodCode.Contains("BANK", StringComparison.OrdinalIgnoreCase) ||
                    row.MethodCode.Contains("TRANSFER", StringComparison.OrdinalIgnoreCase) ||
                    row.MethodName.Contains("CHUYEN KHOAN", StringComparison.OrdinalIgnoreCase))
                {
                    transferIn += row.Amount;
                    continue;
                }

                onlineIn += row.Amount;
            }

            var openingBalance = shift.OpeningBalance;
            var totalCashIn = cashIn;
            var totalCashOut = request?.TotalCashOut ?? shift.TotalCashOut ?? 0m;
            var expectedBalance = openingBalance + totalCashIn - totalCashOut;
            var actualBalance = request?.ActualBalance ?? shift.ActualBalance ?? expectedBalance;
            var discrepancy = actualBalance - expectedBalance;
            var now = VietnamTime.Now;
            var actorId = this.GetCurrentUserId();

            shift.TotalCashIn = totalCashIn;
            shift.TotalCashOut = totalCashOut;
            shift.ExpectedBalance = expectedBalance;
            shift.ActualBalance = actualBalance;
            shift.Discrepancy = discrepancy;
            shift.ClosedAt = now;
            shift.Status = Math.Abs(discrepancy) <= 1000m ? "CLOSED" : "RECONCILED_WITH_VARIANCE";
            shift.Note = BuildShiftNote(shift.Note, request?.Note, transferIn, onlineIn);
            shift.ManagerId = request?.ManagerId ?? shift.ManagerId ?? actorId;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                shiftId = shift.Id,
                shift.Status,
                shift.OpeningBalance,
                shift.TotalCashIn,
                transferIn,
                onlineIn,
                shift.TotalCashOut,
                shift.ExpectedBalance,
                shift.ActualBalance,
                shift.Discrepancy,
                shift.ClosedAt
            });
        }

        private static string BuildShiftNote(string? existing, string? note, decimal transferIn, decimal onlineIn)
        {
            var newSummary = $"Đối soát ca: chuyển khoản={transferIn:#,##0.##}; online={onlineIn:#,##0.##}";
            var extraNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            if (string.IsNullOrWhiteSpace(existing))
            {
                return string.IsNullOrWhiteSpace(extraNote) ? newSummary : $"{newSummary} | {extraNote}";
            }

            return string.IsNullOrWhiteSpace(extraNote)
                ? $"{existing.Trim()} | {newSummary}"
                : $"{existing.Trim()} | {newSummary} | {extraNote}";
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






