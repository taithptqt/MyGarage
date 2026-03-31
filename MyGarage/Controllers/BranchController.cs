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
    [Route("api/[controller]")]
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Obsolete("Use BranchesController with route /api/branches.")]
    public class BranchController : ControllerBase
    {
        private readonly IBranchService _branchService;
        private readonly IMapper _mapper;

        public BranchController(IBranchService branchService, IMapper mapper)
        {
            _branchService = branchService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BranchResponseDto>>> GetAll()
        {
            var items = await _branchService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<BranchResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BranchResponseDto>> GetById(long id)
        {
            var item = await _branchService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<BranchResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<BranchResponseDto>> Create([FromBody] BranchCreateDto itemDto)
        {
            var entity = _mapper.Map<Branch>(itemDto);
            var createdItem = await _branchService.AddAsync(entity);
            var responseDto = _mapper.Map<BranchResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<BranchResponseDto>> Update(long id, [FromBody] BranchUpdateDto itemDto)
        {
            var existingItem = await _branchService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _branchService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<BranchResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _branchService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





