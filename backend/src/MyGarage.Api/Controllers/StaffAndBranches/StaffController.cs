using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Security;

namespace MyGarage.Controllers
{
    [Route("api/v1/staffs")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class StaffController : ControllerBase
    {
        private readonly IStaffService _staffService;
        private readonly ICloudStorageService _cloudStorageService;
        private readonly IFileSecurityScanner _fileSecurityScanner;
        private readonly IStaffQueryService _staffQueryService;

        public StaffController(
            IStaffService staffService,
            ICloudStorageService cloudStorageService,
            IFileSecurityScanner fileSecurityScanner,
            IStaffQueryService staffQueryService)
        {
            _staffService = staffService;
            _cloudStorageService = cloudStorageService;
            _fileSecurityScanner = fileSecurityScanner;
            _staffQueryService = staffQueryService;
        }

        [HttpGet]
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

            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _staffQueryService.GetPagedAsync(scopedBranchId, teamId, normalizedPage, normalizedSize);
            var dtos = _staffService.MapToDtos(paged);
            return Ok(PagedEnvelope<StaffResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("admin-list")]
        public async Task<IActionResult> GetAdminList([FromQuery] long? branchId, [FromQuery] string? search,
            [FromQuery] int? page, [FromQuery] int? pageSize)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied)) return denied!;
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
            var result = await _staffQueryService.GetAdminListAsync(scopedBranchId, search, normalizedPage, normalizedSize);
            return Ok(new
            {
                items = _staffService.MapToDtos(result.Items),
                total = result.Total,
                totalStaff = result.TotalStaff,
                activeStaff = result.ActiveStaff,
                rolesUsed = result.RolesUsed
            });
        }

        [HttpGet("{id:long}")]
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

            var dto = _staffService.MapToDto(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<StaffResponseDto>> Create([FromBody] StaffCreateDto itemDto)
        {
            if (!this.TryResolveRequiredBranchId(itemDto?.BranchId > 0 ? itemDto.BranchId : null, out var targetBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var responseDto = await _staffService.CreateDtoAsync(itemDto!, targetBranchId);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
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

            var responseDto = await _staffService.UpdateDtoAsync(id, itemDto, scopedBranchId);
            if (responseDto == null)
            {
                return NotFound();
            }
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
                return this.BadRequestProblem("Tệp ảnh không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return this.BadRequestProblem("Chỉ chấp nhận tệp ảnh.");
            }

            var scan = await _fileSecurityScanner.ScanFormFileAsync(
                file,
                maxBytes: 5 * 1024 * 1024,
                cancellationToken: HttpContext.RequestAborted);
            if (!scan.IsClean)
            {
                return this.BadRequestProblem(scan.ErrorMessage ?? "Tệp không hợp lệ.");
            }

            var staff = await _staffService.GetByIdAsync(id);
            if (staff == null)
            {
                return this.NotFoundProblem("Không tìm thấy nhân viên.");
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








