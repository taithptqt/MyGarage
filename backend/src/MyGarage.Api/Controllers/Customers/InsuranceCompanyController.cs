using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/insurance-companies")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class InsuranceCompanyController : ControllerBase
    {
        private readonly IInsuranceCompanyService _insuranceCompanyService;

        public InsuranceCompanyController(IInsuranceCompanyService insuranceCompanyService)
        {
            _insuranceCompanyService = insuranceCompanyService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InsuranceCompanyResponseDto>>> GetAll()
        {
            var dtos = await _insuranceCompanyService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InsuranceCompanyResponseDto>> GetById(long id)
        {
            var dto = await _insuranceCompanyService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<InsuranceCompanyResponseDto>> Create([FromBody] InsuranceCompanyCreateDto itemDto)
        {
            var responseDto = await _insuranceCompanyService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InsuranceCompanyResponseDto>> Update(long id, [FromBody] InsuranceCompanyUpdateDto itemDto)
        {
            var responseDto = await _insuranceCompanyService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _insuranceCompanyService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






