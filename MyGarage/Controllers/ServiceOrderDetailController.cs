using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;

namespace MyGarage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceOrderDetailController : ControllerBase
    {
        private readonly IServiceOrderDetailService _serviceOrderDetailService;
        private readonly IMapper _mapper;
        private readonly GarageContext _context;

        public ServiceOrderDetailController(IServiceOrderDetailService serviceOrderDetailService, IMapper mapper, GarageContext context)
        {
            _serviceOrderDetailService = serviceOrderDetailService;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrderDetailResponseDto>>> GetAll([FromQuery] long? serviceOrderId)
        {
            IEnumerable<ServiceOrderDetail> items;
            if (serviceOrderId.HasValue)
            {
                items = await _context.ServiceOrderDetails
                    .Where(x => x.ServiceOrderId == serviceOrderId.Value)
                    .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? System.DateTime.MinValue)
                    .ToListAsync();
            }
            else
            {
                items = await _serviceOrderDetailService.GetAllAsync();
            }
            var dtos = _mapper.Map<IEnumerable<ServiceOrderDetailResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> GetById(long id)
        {
            var item = await _serviceOrderDetailService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ServiceOrderDetailResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> Create([FromBody] ServiceOrderDetailCreateDto itemDto)
        {
            var entity = _mapper.Map<ServiceOrderDetail>(itemDto);
            if (string.IsNullOrWhiteSpace(entity.PayerType))
            {
                entity.PayerType = "CUSTOMER";
            }
            var createdItem = await _serviceOrderDetailService.AddAsync(entity);
            await RecalculateEstimatedHandoverAsync(createdItem.ServiceOrderId);
            var responseDto = _mapper.Map<ServiceOrderDetailResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> Update(long id, [FromBody] ServiceOrderDetailUpdateDto itemDto)
        {
            var existingItem = await _serviceOrderDetailService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _serviceOrderDetailService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            await RecalculateEstimatedHandoverAsync(updatedItem.ServiceOrderId);
            var responseDto = _mapper.Map<ServiceOrderDetailResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var existingItem = await _serviceOrderDetailService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }
            var result = await _serviceOrderDetailService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            await RecalculateEstimatedHandoverAsync(existingItem.ServiceOrderId);
            return NoContent();
        }

        private async Task RecalculateEstimatedHandoverAsync(long serviceOrderId)
        {
            var order = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
            if (order == null || !order.CheckInTime.HasValue)
            {
                return;
            }

            var detailRows = await _context.ServiceOrderDetails
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Where(x => x.ItemType == "LABOR" || x.ItemType == "SERVICE")
                .ToListAsync();

            if (detailRows.Count == 0)
            {
                return;
            }

            var standardServiceIds = detailRows
                .Where(x => x.StandardServiceId.HasValue)
                .Select(x => x.StandardServiceId!.Value)
                .Distinct()
                .ToList();

            var minutesByServiceId = await _context.StandardServices
                .Where(x => standardServiceIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.StandardMinutes);

            var totalMinutes = 0;
            foreach (var detail in detailRows)
            {
                if (!detail.StandardServiceId.HasValue) continue;
                if (!minutesByServiceId.TryGetValue(detail.StandardServiceId.Value, out var standardMinutes)) continue;

                var qty = detail.Quantity;
                if (qty <= 0) qty = 1;
                totalMinutes += (int)Math.Ceiling(standardMinutes * (double)qty);
            }

            if (totalMinutes <= 0)
            {
                return;
            }

            var estimated = AddWorkingMinutes(order.CheckInTime.Value, totalMinutes);
            order.EstimatedHandover = estimated;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private static DateTime AddWorkingMinutes(DateTime start, int minutes)
        {
            var current = start;
            var remaining = Math.Max(0, minutes);

            while (remaining > 0)
            {
                current = AlignToWorkingWindow(current);
                var closeToday = new DateTime(current.Year, current.Month, current.Day, 20, 0, 0, current.Kind);
                var available = (int)Math.Floor((closeToday - current).TotalMinutes);
                if (available <= 0)
                {
                    current = current.Date.AddDays(1).AddHours(8);
                    continue;
                }

                var consume = Math.Min(remaining, available);
                current = current.AddMinutes(consume);
                remaining -= consume;

                if (remaining > 0)
                {
                    current = current.Date.AddDays(1).AddHours(8);
                }
            }

            return AlignToWorkingWindow(current);
        }

        private static DateTime AlignToWorkingWindow(DateTime value)
        {
            var current = value;
            while (current.DayOfWeek == DayOfWeek.Sunday)
            {
                current = current.Date.AddDays(1).AddHours(8);
            }

            var open = new DateTime(current.Year, current.Month, current.Day, 8, 0, 0, current.Kind);
            var close = new DateTime(current.Year, current.Month, current.Day, 20, 0, 0, current.Kind);
            if (current < open) return open;
            if (current >= close)
            {
                var next = current.Date.AddDays(1).AddHours(8);
                while (next.DayOfWeek == DayOfWeek.Sunday)
                {
                    next = next.Date.AddDays(1).AddHours(8);
                }
                return next;
            }

            return current;
        }
    }
}





