using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Authorize(Roles = "Staff")]
    [Route("api/salary-coefficients")]
    [Route("api/v1/salary-coefficients")]
    [ApiController]
    public class SalaryCoefficientController : ControllerBase
    {
        private readonly ISalaryCoefficientService _salaryCoefficientService;
        private readonly IMapper _mapper;

        public SalaryCoefficientController(ISalaryCoefficientService salaryCoefficientService, IMapper mapper)
        {
            _salaryCoefficientService = salaryCoefficientService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SalaryCoefficientResponseDto>>> GetAll()
        {
            var items = await _salaryCoefficientService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<SalaryCoefficientResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SalaryCoefficientResponseDto>> GetById(long id)
        {
            var item = await _salaryCoefficientService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<SalaryCoefficientResponseDto>(item);
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
                var entity = _mapper.Map<SalaryCoefficient>(itemDto);
                var createdItem = await _salaryCoefficientService.AddAsync(entity);
                var responseDto = _mapper.Map<SalaryCoefficientResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SalaryCoefficientResponseDto>> Update(long id, [FromBody] SalaryCoefficientUpdateDto itemDto)
        {
            if (!IsAdminStaff())
            {
                return Forbid();
            }

            var existingItem = await _salaryCoefficientService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            try
            {
                _mapper.Map(itemDto, existingItem);
                var updatedItem = await _salaryCoefficientService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = _mapper.Map<SalaryCoefficientResponseDto>(updatedItem);
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
