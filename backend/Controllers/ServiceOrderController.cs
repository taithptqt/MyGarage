using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/service-orders")]
    [ApiController]
    public class ServiceOrderController : ControllerBase
    {
        private static readonly HashSet<string> AllowedServiceOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "NEW",
            "CHECKED_IN",
            "WAITING",
            "INSPECTING",
            "DIAGNOSING",
            "QUOTING",
            "WAITING_APPROVAL",
            "APPROVED",
            "IN_PROGRESS",
            "REPAIRING",
            "QC",
            "QC_PENDING",
            "READY",
            "WAITING_PAYMENT",
            "CLOSED",
            "CANCELED",
            "CANCELLED",
            "RECEPTION",
            "COMPLETED",
        };

        private static readonly HashSet<string> AllowedPaymentStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "UNPAID",
            "PARTIAL",
            "PAID",
        };

        private static readonly HashSet<string> AllowedDepositStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "NOT_REQUIRED",
            "PENDING",
            "PARTIAL",
            "PAID",
            "REFUNDED",
        };

        private static readonly HashSet<string> AllowedQcStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "PENDING",
            "PASSED",
            "FAILED",
        };

        private readonly IServiceOrderService _serviceOrderService;
        private readonly IMapper _mapper;

        public ServiceOrderController(IServiceOrderService serviceOrderService, IMapper mapper)
        {
            _serviceOrderService = serviceOrderService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrderResponseDto>>> GetAll()
        {
            var items = await _serviceOrderService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ServiceOrderResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderResponseDto>> GetById(long id)
        {
            var item = await _serviceOrderService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ServiceOrderResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderResponseDto>> Create([FromBody] ServiceOrderCreateDto itemDto)
        {
            var statusValidationError = NormalizeAndValidateStatusFields(itemDto);
            if (!string.IsNullOrWhiteSpace(statusValidationError))
            {
                return BadRequest(new { message = statusValidationError });
            }

            var entity = _mapper.Map<ServiceOrder>(itemDto);
            var createdItem = await _serviceOrderService.AddAsync(entity);
            var responseDto = _mapper.Map<ServiceOrderResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderResponseDto>> Update(long id, [FromBody] ServiceOrderUpdateDto itemDto)
        {
            var existingItem = await _serviceOrderService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var statusValidationError = NormalizeAndValidateStatusFields(itemDto);
            if (!string.IsNullOrWhiteSpace(statusValidationError))
            {
                return BadRequest(new { message = statusValidationError });
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _serviceOrderService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<ServiceOrderResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _serviceOrderService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        private static string? NormalizeAndValidateStatusFields(ServiceOrderCreateDto dto)
        {
            if (dto == null)
            {
                return "Dữ liệu Service Order không hợp lệ.";
            }

            var error = TryNormalizeChoice(dto.Status, AllowedServiceOrderStatuses, "status", out var normalizedStatus);
            if (error != null) return error;
            if (normalizedStatus != null) dto.Status = normalizedStatus;

            error = TryNormalizeChoice(dto.PaymentStatus, AllowedPaymentStatuses, "paymentStatus", out var normalizedPaymentStatus);
            if (error != null) return error;
            if (normalizedPaymentStatus != null) dto.PaymentStatus = normalizedPaymentStatus;

            error = TryNormalizeChoice(dto.DepositStatus, AllowedDepositStatuses, "depositStatus", out var normalizedDepositStatus);
            if (error != null) return error;
            if (normalizedDepositStatus != null) dto.DepositStatus = normalizedDepositStatus;

            error = TryNormalizeChoice(dto.Qcstatus, AllowedQcStatuses, "qcStatus", out var normalizedQcStatus);
            if (error != null) return error;
            if (normalizedQcStatus != null) dto.Qcstatus = normalizedQcStatus;

            return null;
        }

        private static string? NormalizeAndValidateStatusFields(ServiceOrderUpdateDto dto)
        {
            if (dto == null)
            {
                return "Dữ liệu Service Order không hợp lệ.";
            }

            var error = TryNormalizeChoice(dto.Status, AllowedServiceOrderStatuses, "status", out var normalizedStatus);
            if (error != null) return error;
            if (normalizedStatus != null) dto.Status = normalizedStatus;

            error = TryNormalizeChoice(dto.PaymentStatus, AllowedPaymentStatuses, "paymentStatus", out var normalizedPaymentStatus);
            if (error != null) return error;
            if (normalizedPaymentStatus != null) dto.PaymentStatus = normalizedPaymentStatus;

            error = TryNormalizeChoice(dto.DepositStatus, AllowedDepositStatuses, "depositStatus", out var normalizedDepositStatus);
            if (error != null) return error;
            if (normalizedDepositStatus != null) dto.DepositStatus = normalizedDepositStatus;

            error = TryNormalizeChoice(dto.Qcstatus, AllowedQcStatuses, "qcStatus", out var normalizedQcStatus);
            if (error != null) return error;
            if (normalizedQcStatus != null) dto.Qcstatus = normalizedQcStatus;

            return null;
        }

        private static string? TryNormalizeChoice(
            string? rawValue,
            HashSet<string> allowedValues,
            string fieldName,
            out string? normalizedValue)
        {
            normalizedValue = null;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var token = rawValue.Trim().ToUpperInvariant();
            if (!allowedValues.Contains(token))
            {
                var allowed = string.Join(", ", allowedValues.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                return $"Giá trị {fieldName} không hợp lệ: {rawValue}. Chấp nhận: {allowed}.";
            }

            normalizedValue = token;
            return null;
        }
    }
}






