using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentBookingCoreService
    {
        Task<Appointment> BookCoreAsync(
            long authUserId,
            BookAppointmentRequestDto dto,
            long? groupSessionId);

        Task EnsureBatchFixedLeadTimesAsync(
            long branchId,
            IReadOnlyCollection<BookAppointmentRequestDto> items);
    }
}
