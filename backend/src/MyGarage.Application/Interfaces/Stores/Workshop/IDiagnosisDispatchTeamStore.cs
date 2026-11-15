using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IDiagnosisDispatchTeamStore
{
    Task<DiagnosisTeamProfileRow?> GetTeamProfileAsync(long teamId);
    Task<DiagnosisDispatchTeamRow?> GetDispatchTeamByIdAsync(long teamId);
    Task<IReadOnlyList<DiagnosisDispatchTeamRow>> GetTeamsByBranchNoTrackingAsync(long branchId);
    Task<IReadOnlyDictionary<long, string>> GetTeamNameMapAsync(IReadOnlyCollection<long> teamIds);
    Task<string?> GetTeamSpecialtyTagAsync(long teamId);
    Task<long?> GetTeamLeaderIdAsync(long teamId);
    Task<Team?> GetTeamByIdAsync(long teamId);
}
