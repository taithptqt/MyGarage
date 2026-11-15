using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentBookingCommandService
    {
        Task<Appointment> BookAsync(long customerId, BookAppointmentRequestDto dto);
        Task<ReceptionAppointmentSaveResponseDto> SaveReceptionBookingAsync(long actorUserId, ReceptionAppointmentSaveRequestDto dto);
        Task<GroupSessionResponseDto> BookBatchAsync(long customerId, BookAppointmentBatchRequestDto dto);
        Task<GroupSessionResponseDto> BookBatchForCustomerAsync(long actorUserId, long targetCustomerId, BookAppointmentBatchRequestDto dto);
    }
}
