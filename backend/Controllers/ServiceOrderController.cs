using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/service-orders")]
    [ApiController]
    public class ServiceOrderController : ControllerBase
    {
        private static readonly HashSet<string> AllowedServiceOrderStatuses =
            new(Enum.GetNames<ServiceOrderLifecycleStatus>(), StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> AllowedPaymentStatuses =
            new(Enum.GetNames<ServiceOrderPaymentStatusCode>(), StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> AllowedDepositStatuses =
            new(Enum.GetNames<ServiceOrderDepositStatusCode>(), StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> AllowedQcStatuses =
            new(Enum.GetNames<ServiceOrderQcStatusCode>(), StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> AllowedOrderCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "QUICK_SERVICE",
            "GENERAL_REPAIR",
            "MAINTENANCE",
            "INSURANCE_REPAIR",
            "REWORK",
            "WARRANTLY",
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
            var validationError = NormalizeAndValidateFields(itemDto);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return BadRequest(new { message = validationError });
            }

            var entity = _mapper.Map<ServiceOrder>(itemDto);
            var createdItem = await _serviceOrderService.AddAsync(entity);
            var responseDto = _mapper.Map<ServiceOrderResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPost("batches")]
        public async Task<ActionResult<ServiceOrderBatchCreateResponseDto>> CreateBatch([FromBody] ServiceOrderBatchCreateRequestDto dto)
        {
            try
            {
                var actorUserId = this.GetCurrentUserId();
                var result = await _serviceOrderService.AddWorkshopBatchAsync(actorUserId, dto);
                var mappedOrders = _mapper.Map<List<ServiceOrderResponseDto>>(result.Orders);
                var response = new ServiceOrderBatchCreateResponseDto
                {
                    GroupSessionId = result.GroupSessionId,
                    TotalCreated = mappedOrders.Count,
                    Orders = mappedOrders
                };

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}/workshop")]
        public async Task<ActionResult<ServiceOrderResponseDto>> UpdateWorkshop(long id, [FromBody] WorkshopServiceOrderEditRequestDto dto)
        {
            try
            {
                var actorUserId = this.GetCurrentUserId();
                var updatedItem = await _serviceOrderService.UpdateWorkshopAsync(id, dto, actorUserId);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = _mapper.Map<ServiceOrderResponseDto>(updatedItem);
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderResponseDto>> Update(long id, [FromBody] ServiceOrderUpdateDto itemDto)
        {
            var existingItem = await _serviceOrderService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var validationError = NormalizeAndValidateFields(itemDto);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return BadRequest(new { message = validationError });
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

        private static string? NormalizeAndValidateFields(ServiceOrderCreateDto dto)
        {
            if (dto == null)
            {
                return "Du lieu Service Order khong hop le.";
            }

            var error = TryNormalizeOrderCategory(dto.OrderCategory, out var normalizedOrderCategory);
            if (error != null) return error;
            if (normalizedOrderCategory != null) dto.OrderCategory = normalizedOrderCategory;

            error = TryNormalizeChoice(dto.Status, AllowedServiceOrderStatuses, "status", out var normalizedStatus);
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

        private static string? NormalizeAndValidateFields(ServiceOrderUpdateDto dto)
        {
            if (dto == null)
            {
                return "Du lieu Service Order khong hop le.";
            }

            var error = TryNormalizeOrderCategory(dto.OrderCategory, out var normalizedOrderCategory);
            if (error != null) return error;
            if (normalizedOrderCategory != null) dto.OrderCategory = normalizedOrderCategory;

            error = TryNormalizeChoice(dto.Status, AllowedServiceOrderStatuses, "status", out var normalizedStatus);
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

            var token = NormalizeToken(rawValue);
            if (!allowedValues.Contains(token))
            {
                var allowed = string.Join(", ", allowedValues.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                return $"Gia tri {fieldName} khong hop le: {rawValue}. Chap nhan: {allowed}.";
            }

            normalizedValue = token;
            return null;
        }

        private static string? TryNormalizeOrderCategory(string? rawValue, out string? normalizedValue)
        {
            normalizedValue = null;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var token = NormalizeToken(rawValue);
            if (!AllowedOrderCategories.Contains(token))
            {
                var allowed = string.Join(", ", AllowedOrderCategories.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                return $"Gia tri orderCategory khong hop le: {rawValue}. Chap nhan: {allowed}.";
            }

            normalizedValue = token;
            return null;
        }

        private static string NormalizeToken(string value)
        {
            return value.Trim()
                .ToUpperInvariant()
                .Replace("-", "_")
                .Replace(" ", "_");
        }
    }
}
