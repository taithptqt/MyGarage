using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/insurance-companies")]
[Route("api/[controller]")]
    [ApiController]
    public class InsuranceCompanyController : ControllerBase
    {
        private readonly IInsuranceCompanyService _insuranceCompanyService;
        private readonly IMapper _mapper;

        public InsuranceCompanyController(IInsuranceCompanyService insuranceCompanyService, IMapper mapper)
        {
            _insuranceCompanyService = insuranceCompanyService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InsuranceCompanyResponseDto>>> GetAll()
        {
            var items = await _insuranceCompanyService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<InsuranceCompanyResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InsuranceCompanyResponseDto>> GetById(long id)
        {
            var item = await _insuranceCompanyService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<InsuranceCompanyResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<InsuranceCompanyResponseDto>> Create([FromBody] InsuranceCompanyCreateDto itemDto)
        {
            var entity = _mapper.Map<InsuranceCompany>(itemDto);
            var createdItem = await _insuranceCompanyService.AddAsync(entity);
            var responseDto = _mapper.Map<InsuranceCompanyResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InsuranceCompanyResponseDto>> Update(long id, [FromBody] InsuranceCompanyUpdateDto itemDto)
        {
            var existingItem = await _insuranceCompanyService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _insuranceCompanyService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<InsuranceCompanyResponseDto>(updatedItem);
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






