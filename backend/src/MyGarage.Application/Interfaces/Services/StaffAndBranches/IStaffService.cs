using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public enum StaffAvatarUploadStatus
    {
        Ok,
        NotFound,
        Forbidden
    }

    public sealed record StaffAvatarUploadResult(
        StaffAvatarUploadStatus Status,
        string? AvatarUrl = null);

    public interface IStaffService : IGenericService<Staff>
    {
        List<StaffResponseDto> MapToDtos(IEnumerable<Staff> items);
        StaffResponseDto MapToDto(Staff item);
        Task<StaffResponseDto> CreateDtoAsync(StaffCreateDto itemDto, long branchId);
        Task<StaffResponseDto?> UpdateDtoAsync(long id, StaffUpdateDto itemDto, long? scopedBranchId = null);
        Task<StaffAvatarUploadResult> UploadAvatarAsync(
            long id,
            long? scopedBranchId,
            string? originalFileName,
            string contentType,
            byte[] fileBytes,
            CancellationToken ct = default);
    }
}
