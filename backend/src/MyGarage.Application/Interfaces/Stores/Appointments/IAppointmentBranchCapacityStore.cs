using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Appointments;

public interface IAppointmentBranchCapacityStore
{
    Task<BranchOperatingHour?> GetBranchOperatingHourAsync(long branchId, int dayOfWeek);
    Task<BranchException?> GetBranchExceptionAsync(long branchId, DateOnly date);
    Task<Branch?> GetActiveBranchByIdAsync(long branchId);
    Task<List<Branch>> GetActiveBranchesExceptAsync(long excludedBranchId);
    Task<List<Bay>> GetActiveBaysAsync(long branchId, bool ordered = false);
    Task<int> CountActiveBaysAsync(long branchId);
    Task<List<CapacityConfig>> GetActiveCapacityConfigsForDayAsync(long branchId, byte normalizedDayOfWeek);
    AppointmentLifecycleConfigSnapshot GetLifecycleConfig(long branchId, bool isWeekend);
}
