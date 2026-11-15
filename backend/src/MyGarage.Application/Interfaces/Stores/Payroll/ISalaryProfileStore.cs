using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Payroll.DTOs;
using MyGarage.Models;

namespace MyGarage.Application.Payroll;

public interface ISalaryProfileStore
{
    Task<(IReadOnlyList<SalaryProfileDto> Items, int TotalCount)> GetSalaryProfilesPageAsync(
        long? branchId,
        long? staffId,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<StaffSalaryProfile?> GetSalaryProfileWithRelationsAsync(long id, bool track = false, CancellationToken ct = default);

    Task<Staff?> GetActiveStaffAsync(long staffId, CancellationToken ct = default);

    Task<IReadOnlyList<StaffSalaryProfile>> GetActiveSalaryProfilesForStaffAsync(
        long staffId,
        long? excludeProfileId = null,
        CancellationToken ct = default);

    Task AddSalaryProfileAsync(StaffSalaryProfile profile, CancellationToken ct = default);

    Task RemoveSalaryProfileAsync(StaffSalaryProfile profile, CancellationToken ct = default);

    Task<IReadOnlyList<SalaryRequestDto>> GetSalaryRequestsAsync(CancellationToken ct = default);

    Task<bool> HasSalaryRequestCrudSupportAsync();

    Task<string?> GetSalaryRequestStatusAsync(long requestId, CancellationToken ct = default);

    Task ApproveSalaryRequestAsync(
        long requestId,
        long? approverId,
        DateTime now,
        StaffSalaryProfile? createdProfile,
        CancellationToken ct = default);

    Task RejectSalaryRequestAsync(
        long requestId,
        long? approverId,
        string rejectReason,
        DateTime now,
        CancellationToken ct = default);

    Task<IReadOnlyList<StaffSalaryProfile>> GetOverlappingSalaryProfilesAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);
}
