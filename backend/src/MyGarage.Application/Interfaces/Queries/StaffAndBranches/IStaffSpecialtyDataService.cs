using MyGarage.DTOs.Responses;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record StaffSpecialtyUpdateResult(long StaffId, string? Specialty, IReadOnlyList<string> Specialties);

public interface IStaffSpecialtyDataService
{
    Task<StaffSpecialtyUpdateResult?> UpdateSpecialtiesAsync(
        long id,
        IReadOnlyList<string>? specialties,
        long? updatedBy,
        CancellationToken ct = default);

    Task<IReadOnlyList<StaffAvailabilityResponseDto>> GetAvailableStaffAsync(
        string? specialty,
        DateTime? date,
        long? branchId,
        CancellationToken ct = default);
}
