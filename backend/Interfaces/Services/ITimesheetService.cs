using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.DTOs.Responses.Execution;

namespace MyGarage.Interfaces
{
    public interface ITimesheetService
    {
        Task<IReadOnlyList<TimesheetEntryDto>> GetTodayEntriesAsync(long staffId);
        Task<TimesheetEntryDto> CheckInAsync(TimesheetCheckInRequestDto request);
        Task<TimesheetEntryDto> CheckOutAsync(long timesheetId, TimesheetCheckOutRequestDto request);
    }
}
