using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/standard-services")]
    [ApiController]
    public class StandardServicesController : ControllerBase
    {
        private readonly IStandardServiceService _standardServiceService;
        private readonly IMapper _mapper;
        private const string PackageCodePrefix = "PKG-";

        public StandardServicesController(IStandardServiceService standardServiceService, IMapper mapper)
        {
            _standardServiceService = standardServiceService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StandardServiceResponseDto>>> GetAll()
        {
            var items = await _standardServiceService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<StandardServiceResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<StandardServiceResponseDto>> GetById(long id)
        {
            var item = await _standardServiceService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<StandardServiceResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<StandardServiceResponseDto>> Create([FromBody] StandardServiceCreateDto itemDto)
        {
            var invalidCodeResult = ValidateStandardServiceCode(itemDto?.Code);
            if (invalidCodeResult != null)
            {
                return invalidCodeResult;
            }

            var entity = _mapper.Map<StandardService>(itemDto);
            var createdItem = await _standardServiceService.AddAsync(entity);
            var responseDto = _mapper.Map<StandardServiceResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<StandardServiceResponseDto>> Update(long id, [FromBody] StandardServiceUpdateDto itemDto)
        {
            var invalidCodeResult = ValidateStandardServiceCode(itemDto?.Code);
            if (invalidCodeResult != null)
            {
                return invalidCodeResult;
            }

            var existingItem = await _standardServiceService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _standardServiceService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            var responseDto = _mapper.Map<StandardServiceResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _standardServiceService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        private ActionResult? ValidateStandardServiceCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest(new { message = "Mã dịch vụ không được để trống." });
            }

            if (code.Trim().StartsWith(PackageCodePrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Mã bắt đầu bằng PKG- phải được quản lý ở bảng Packages, không tạo trong Standard Services."
                });
            }

            return null;
        }
    }
}
