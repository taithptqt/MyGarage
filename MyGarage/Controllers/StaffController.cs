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
    public class StaffController : ControllerBase
    {
        private readonly IStaffService _staffService;
        private readonly IMapper _mapper;

        public StaffController(IStaffService staffService, IMapper mapper)
        {
            _staffService = staffService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StaffResponseDto>>> GetAll()
        {
            var items = await _staffService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<StaffResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StaffResponseDto>> GetById(long id)
        {
            var item = await _staffService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<StaffResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<StaffResponseDto>> Create([FromBody] StaffCreateDto itemDto)
        {
            var entity = _mapper.Map<Staff>(itemDto);
            var createdItem = await _staffService.AddAsync(entity);
            var responseDto = _mapper.Map<StaffResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<StaffResponseDto>> Update(long id, [FromBody] StaffUpdateDto itemDto)
        {
            var existingItem = await _staffService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);

            var currentUserIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserIdStr != id.ToString())
            {
                existingItem.SecurityStamp = Guid.NewGuid().ToString();
            }

            var updatedItem = await _staffService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<StaffResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _staffService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpPost("{id}/avatar")]
        public async Task<IActionResult> UploadAvatar(long id, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var staff = await _staffService.GetByIdAsync(id);
            if (staff == null)
                return NotFound("Staff not found.");

            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension)) extension = ".jpg";
            var fileName = $"{id}_{DateTime.Now.Ticks}{extension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Update staff avatar url
            staff.AvatarUrl = $"/uploads/avatars/{fileName}";
            await _staffService.UpdateAsync(id, staff);

            return Ok(new { avatarUrl = staff.AvatarUrl });
        }
    }
}





