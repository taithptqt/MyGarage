using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Authorize(Roles = "Staff")]
    [Route("api/v1/salary-coefficients")]
    [ApiController]
    public class SalaryCoefficientController : ControllerBase
    {
        private readonly ISalaryCoefficientService _salaryCoefficientService;

        public SalaryCoefficientController(ISalaryCoefficientService salaryCoefficientService)
        {
            _salaryCoefficientService = salaryCoefficientService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SalaryCoefficientResponseDto>>> GetAll()
        {
            var dtos = await _salaryCoefficientService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SalaryCoefficientResponseDto>> GetById(long id)
        {
            var dto = await _salaryCoefficientService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<SalaryCoefficientResponseDto>> Create([FromBody] SalaryCoefficientCreateDto itemDto)
        {
            if (!IsAdminStaff())
            {
                return Forbid();
            }

            try
            {
                var responseDto = await _salaryCoefficientService.CreateDtoAsync(itemDto);
                return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SalaryCoefficientResponseDto>> Update(long id, [FromBody] SalaryCoefficientUpdateDto itemDto)
        {
            if (!IsAdminStaff())
            {
                return Forbid();
            }

            try
            {
                var responseDto = await _salaryCoefficientService.UpdateDtoAsync(id, itemDto);
                if (responseDto == null)
                {
                    return NotFound();
                }

                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!IsAdminStaff())
            {
                return Forbid();
            }

            var result = await _salaryCoefficientService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        private bool IsAdminStaff()
        {
            var roleCode = this.GetCurrentRoleCode();
            var roleName = this.GetCurrentRoleName();

            return string.Equals(roleCode, "ADMIN", StringComparison.OrdinalIgnoreCase)
                   || (roleName?.Contains("admin", StringComparison.OrdinalIgnoreCase) ?? false);
        }
    }
}
