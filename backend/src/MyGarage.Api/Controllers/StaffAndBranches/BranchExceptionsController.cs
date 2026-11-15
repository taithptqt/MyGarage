using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/branch-exceptions")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class BranchExceptionsController : ControllerBase
    {
        private readonly IBranchExceptionService _branchExceptionService;

        public BranchExceptionsController(IBranchExceptionService branchExceptionService)
        {
            _branchExceptionService = branchExceptionService;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BranchExceptionResponseDto>>> GetAll()
        {
            var dtos = await _branchExceptionService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [AllowAnonymous]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<BranchExceptionResponseDto>> GetById(long id)
        {
            var dto = await _branchExceptionService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<BranchExceptionResponseDto>> Create([FromBody] BranchExceptionCreateDto itemDto)
        {
            var responseDto = await _branchExceptionService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<BranchExceptionResponseDto>> Update(long id, [FromBody] BranchExceptionUpdateDto itemDto)
        {
            var responseDto = await _branchExceptionService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }

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
