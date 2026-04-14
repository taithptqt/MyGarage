using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/staffs")]
    [ApiController]
    public class StaffController : ControllerBase
    {
        private readonly IStaffService _staffService;
        private readonly IMapper _mapper;
        private readonly ICloudStorageService _cloudStorageService;

        public StaffController(IStaffService staffService, IMapper mapper, ICloudStorageService cloudStorageService)
        {
            _staffService = staffService;
            _mapper = mapper;
            _cloudStorageService = cloudStorageService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StaffResponseDto>>> GetAll([FromQuery] long? branchId = null, [FromQuery] long? teamId = null)
        {
            var items = await _staffService.GetAllAsync();

            var accountantBranchId = this.IsCurrentAccountingStaff() ? this.GetCurrentBranchId() : null;
            if (this.IsCurrentAccountingStaff() && !accountantBranchId.HasValue)
            {
                return Forbid();
            }

            var effectiveBranchId = accountantBranchId ?? branchId;
            if (effectiveBranchId.HasValue)
            {
                items = items.Where(s => s.BranchId == effectiveBranchId.Value).ToList();
            }

            if (teamId.HasValue)
            {
                items = items.Where(s => s.TeamId == teamId.Value).ToList();
            }

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

            var accountantBranchId = this.IsCurrentAccountingStaff() ? this.GetCurrentBranchId() : null;
            if (this.IsCurrentAccountingStaff() && !accountantBranchId.HasValue)
            {
                return Forbid();
            }

            if (accountantBranchId.HasValue && item.BranchId != accountantBranchId.Value)
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

        [HttpPost("{id}/avatars")]
        public async Task<ActionResult<object>> UploadAvatar(long id, [FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Tap anh khong hop le." });
            }

            if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chi chap nhan tap anh." });
            }

            var staff = await _staffService.GetByIdAsync(id);
            if (staff == null)
            {
                return NotFound(new { message = "Khong tim thay nhan vien." });
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            var fileName = $"{VietnamTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
            await using var stream = file.OpenReadStream();
            var avatarUrl = await _cloudStorageService.UploadAsync(
                stream,
                "avatars/staff",
                fileName,
                file.ContentType,
                HttpContext.RequestAborted);

            staff.AvatarUrl = avatarUrl;
            await _staffService.UpdateAsync(id, staff);

            return Ok(new { avatarUrl });
        }
    }
}
