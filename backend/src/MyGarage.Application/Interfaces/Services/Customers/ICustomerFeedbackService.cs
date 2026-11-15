using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ICustomerFeedbackService : IGenericService<CustomerFeedback>
    {
        IEnumerable<CustomerFeedbackResponseDto> MapToDtos(IEnumerable<CustomerFeedback> items);
        Task<CustomerFeedbackResponseDto?> GetDtoByIdAsync(long id);
        Task<CustomerFeedbackResponseDto> CreateDtoAsync(CustomerFeedbackCreateDto itemDto, long? overrideCustomerId = null);
        Task<CustomerFeedbackResponseDto?> UpdateDtoAsync(long id, CustomerFeedbackUpdateDto itemDto, bool preserveCustomerId = false);
    }
}
