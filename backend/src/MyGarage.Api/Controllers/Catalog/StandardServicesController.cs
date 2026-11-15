using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/standard-services")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class StandardServicesController : ControllerBase
    {
        private readonly IStandardServiceService _standardServiceService;
        private const string PackageCodePrefix = "PKG-";

        public StandardServicesController(IStandardServiceService standardServiceService)
        {
            _standardServiceService = standardServiceService;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<StandardServiceResponseDto>>> GetAll()
        {
            var dtos = await _standardServiceService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [AllowAnonymous]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<StandardServiceResponseDto>> GetById(long id)
        {
            var dto = await _standardServiceService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

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

            var responseDto = await _standardServiceService.CreateDtoAsync(itemDto!);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<StandardServiceResponseDto>> Update(long id, [FromBody] StandardServiceUpdateDto itemDto)
        {
            var invalidCodeResult = ValidateStandardServiceCode(itemDto?.Code);
            if (invalidCodeResult != null)
            {
                return invalidCodeResult;
            }

            var responseDto = await _standardServiceService.UpdateDtoAsync(id, itemDto!);
            if (responseDto == null)
            {
                return NotFound();
            }

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
                return this.BadRequestProblem("Mã dịch vụ không được để trống.");
            }

            if (code.Trim().StartsWith(PackageCodePrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return this.BadRequestProblem("Mã bắt đầu bằng PKG- phải được quản lý ở bảng Packages, không tạo trong Standard Services.");
            }

            return null;
        }
    }
}
