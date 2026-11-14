using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Security;

namespace MyGarage.Controllers
{
    [Route("api/staffs")]
    [Route("api/v1/staffs")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class StaffController : ControllerBase
    {
        private readonly IStaffService _staffService;
        private readonly IMapper _mapper;
        private readonly ICloudStorageService _cloudStorageService;
        private readonly IFileSecurityScanner _fileSecurityScanner;
        private readonly IGarageContext _context;

        public StaffController(
            IStaffService staffService,
            IMapper mapper,
            ICloudStorageService cloudStorageService,
            IFileSecurityScanner fileSecurityScanner,
            IGarageContext context)
        {
            _staffService = staffService;
            _mapper = mapper;
            _cloudStorageService = cloudStorageService;
            _fileSecurityScanner = fileSecurityScanner;
            _context = context;
        }

        [HttpGet]
        [HttpGet("/api/staff")]
        [HttpGet("/api/v1/staff")]
        public async Task<ActionResult<PagedEnvelope<StaffResponseDto>>> GetAll(
            [FromQuery] long? branchId = null,
            [FromQuery] long? teamId = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var query = _context.Staffs.AsNoTracking().AsQueryable();
            if (scopedBranchId.HasValue)
            {
                query = query.Where(s => s.BranchId == scopedBranchId.Value);
            }
            if (teamId.HasValue)
            {
                query = query.Where(s => s.TeamId == teamId.Value);
            }
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var total = await query.CountAsync();
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var paged = offset >= total
                ? new List<Staff>()
                : await query.OrderBy(s => s.Id).Skip((int)offset).Take(normalizedSize).ToListAsync();
            var dtos = _mapper.Map<List<StaffResponseDto>>(paged);
            return Ok(PagedEnvelope<StaffResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("admin-list")]
        public async Task<IActionResult> GetAdminList([FromQuery] long? branchId, [FromQuery] string? search,
            [FromQuery] int? page, [FromQuery] int? pageSize)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied)) return denied!;
            var baseQuery = _context.Staffs.AsNoTracking().AsQueryable();
            if (scopedBranchId.HasValue) baseQuery = baseQuery.Where(s => s.BranchId == scopedBranchId.Value);
            var totalStaff = await baseQuery.CountAsync();
            var activeStaff = await baseQuery.CountAsync(s => s.Status != null && s.Status.ToLower() == "active");
            var rolesUsed = await baseQuery.Where(s => s.RoleId.HasValue && s.RoleId != 0)
                .Select(s => s.RoleId).Distinct().CountAsync();
            var query = baseQuery;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(s => (s.FullName ?? "").ToLower().Contains(term) ||
                    (s.Email ?? "").ToLower().Contains(term) || (s.Phone ?? "").ToLower().Contains(term) ||
                    (s.Role != null && ((s.Role.RoleName != null && s.Role.RoleName != "" ? s.Role.RoleName : s.Role.RoleCode) ?? "").ToLower().Contains(term)));
            }
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
            var total = await query.CountAsync();
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var items = offset >= total ? new List<Staff>() : await query.OrderBy(s => s.Id)
                .Skip((int)offset).Take(normalizedSize).ToListAsync();
            return Ok(new { items = _mapper.Map<List<StaffResponseDto>>(items), total, totalStaff, activeStaff, rolesUsed });
        }

        [HttpGet("{id:long}")]
        [HttpGet("/api/staff/{id:long}")]
        [HttpGet("/api/v1/staff/{id:long}")]
        public async Task<ActionResult<StaffResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var item = await _staffService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && item.BranchId != scopedBranchId.Value)
            {
                return Forbid();
            }

            var dto = _mapper.Map<StaffResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<StaffResponseDto>> Create([FromBody] StaffCreateDto itemDto)
        {
            if (!this.TryResolveRequiredBranchId(itemDto?.BranchId > 0 ? itemDto.BranchId : null, out var targetBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var entity = _mapper.Map<Staff>(itemDto);
            entity.BranchId = targetBranchId;
            var createdItem = await _staffService.AddAsync(entity);
            var responseDto = _mapper.Map<StaffResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<StaffResponseDto>> Update(long id, [FromBody] StaffUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var existingItem = await _staffService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && existingItem.BranchId != scopedBranchId.Value)
            {
                return Forbid();
            }

            _mapper.Map(itemDto, existingItem);
            if (scopedBranchId.HasValue)
            {
                existingItem.BranchId = scopedBranchId.Value;
            }

            var updatedItem = await _staffService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<StaffResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var existingItem = await _staffService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && existingItem.BranchId != scopedBranchId.Value)
            {
                return Forbid();
            }

            var result = await _staffService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpPost("{id:long}/avatars")]
        public async Task<ActionResult<object>> UploadAvatar(long id, IFormFile file)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Tệp ảnh không hợp lệ." });
            }

            if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chỉ chấp nhận tệp ảnh." });
            }

            var scan = await _fileSecurityScanner.ScanFormFileAsync(
                file,
                maxBytes: 5 * 1024 * 1024,
                cancellationToken: HttpContext.RequestAborted);
            if (!scan.IsClean)
            {
                return BadRequest(new { message = scan.ErrorMessage });
            }

            var staff = await _staffService.GetByIdAsync(id);
            if (staff == null)
            {
                return NotFound(new { message = "Không tìm thấy nhân viên." });
            }

            if (scopedBranchId.HasValue && staff.BranchId != scopedBranchId.Value)
            {
                return Forbid();
            }

            var extension = Path.GetExtension(file!.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            var fileName = $"{VietnamTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
            await using var stream = new MemoryStream(scan.Bytes);
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








