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
    [Route("api/approval-requests")]
[Route("api/[controller]")]
    [ApiController]
    public class ApprovalRequestController : ControllerBase
    {
        private readonly IApprovalRequestService _approvalRequestService;
        private readonly IMapper _mapper;

        public ApprovalRequestController(IApprovalRequestService approvalRequestService, IMapper mapper)
        {
            _approvalRequestService = approvalRequestService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ApprovalRequestResponseDto>>> GetAll()
        {
            var items = await _approvalRequestService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ApprovalRequestResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApprovalRequestResponseDto>> GetById(long id)
        {
            var item = await _approvalRequestService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ApprovalRequestResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ApprovalRequestResponseDto>> Create([FromBody] ApprovalRequestCreateDto itemDto)
        {
            var entity = _mapper.Map<ApprovalRequest>(itemDto);
            var createdItem = await _approvalRequestService.AddAsync(entity);
            var responseDto = _mapper.Map<ApprovalRequestResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApprovalRequestResponseDto>> Update(long id, [FromBody] ApprovalRequestUpdateDto itemDto)
        {
            var existingItem = await _approvalRequestService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _approvalRequestService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<ApprovalRequestResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _approvalRequestService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






