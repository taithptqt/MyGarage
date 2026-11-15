using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/service-order-details")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ServiceOrderDetailController : ControllerBase
    {
        private readonly IServiceOrderDetailCommandService _commandService;

        public ServiceOrderDetailController(IServiceOrderDetailCommandService commandService)
        {
            _commandService = commandService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrderDetailResponseDto>>> GetAll([FromQuery] long? serviceOrderId = null)
        {
            var dtos = await _commandService.GetAllAsync(serviceOrderId, HttpContext.RequestAborted);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> GetById(long id)
        {
            var dto = await _commandService.GetByIdAsync(id, HttpContext.RequestAborted);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> Create([FromBody] ServiceOrderDetailCreateDto itemDto)
        {
            var result = await _commandService.CreateAsync(itemDto, HttpContext.RequestAborted);
            return result.Status switch
            {
                ServiceOrderDetailCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Dữ liệu hạng mục service order không hợp lệ."),
                _ => CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data!)
            };
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> Update(long id, [FromBody] ServiceOrderDetailUpdateDto itemDto)
        {
            var result = await _commandService.UpdateAsync(id, itemDto, HttpContext.RequestAborted);
            return result.Status switch
            {
                ServiceOrderDetailCommandStatus.NotFound => NotFound(),
                ServiceOrderDetailCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Không thể cập nhật hạng mục service order."),
                _ => Ok(result.Data!)
            };
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _commandService.DeleteAsync(id, HttpContext.RequestAborted);
            return result.Status switch
            {
                ServiceOrderDetailCommandStatus.NotFound => NotFound(),
                ServiceOrderDetailCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Không thể xóa hạng mục service order."),
                _ => NoContent()
            };
        }
    }
}
