using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentDetailAssembler
    {
        Task<AppointmentDetailResponseDto> AssembleAsync(Appointment appointment);
        Task<IEnumerable<AppointmentDetailResponseDto>> AssembleManyAsync(IEnumerable<Appointment> appointments);
    }
}
