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
    public class CustomerFeedbackController : ControllerBase
    {
        private readonly ICustomerFeedbackService _customerFeedbackService;
        private readonly IMapper _mapper;

        public CustomerFeedbackController(ICustomerFeedbackService customerFeedbackService, IMapper mapper)
        {
            _customerFeedbackService = customerFeedbackService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerFeedbackResponseDto>>> GetAll()
        {
            var items = await _customerFeedbackService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<CustomerFeedbackResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerFeedbackResponseDto>> GetById(long id)
        {
            var item = await _customerFeedbackService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<CustomerFeedbackResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CustomerFeedbackResponseDto>> Create([FromBody] CustomerFeedbackCreateDto itemDto)
        {
            var entity = _mapper.Map<CustomerFeedback>(itemDto);
            var createdItem = await _customerFeedbackService.AddAsync(entity);
            var responseDto = _mapper.Map<CustomerFeedbackResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CustomerFeedbackResponseDto>> Update(long id, [FromBody] CustomerFeedbackUpdateDto itemDto)
        {
            var existingItem = await _customerFeedbackService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _customerFeedbackService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<CustomerFeedbackResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _customerFeedbackService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





