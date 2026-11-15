using System.Collections.Generic;
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
    [Route("api/branch-exceptions")]
    [Route("api/v1/branch-exceptions")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class BranchExceptionsController : ControllerBase
    {
        private readonly IBranchExceptionService _branchExceptionService;
        private readonly IMapper _mapper;

        public BranchExceptionsController(IBranchExceptionService branchExceptionService, IMapper mapper)
        {
            _branchExceptionService = branchExceptionService;
            _mapper = mapper;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BranchExceptionResponseDto>>> GetAll()
        {
            var items = await _branchExceptionService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<BranchExceptionResponseDto>>(items);
            return Ok(dtos);
        }

        [AllowAnonymous]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<BranchExceptionResponseDto>> GetById(long id)
        {
            var item = await _branchExceptionService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<BranchExceptionResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<BranchExceptionResponseDto>> Create([FromBody] BranchExceptionCreateDto itemDto)
        {
            var entity = _mapper.Map<BranchException>(itemDto);
            var createdItem = await _branchExceptionService.AddAsync(entity);
            var responseDto = _mapper.Map<BranchExceptionResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<BranchExceptionResponseDto>> Update(long id, [FromBody] BranchExceptionUpdateDto itemDto)
        {
            var existingItem = await _branchExceptionService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _branchExceptionService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            var responseDto = _mapper.Map<BranchExceptionResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _branchExceptionService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
