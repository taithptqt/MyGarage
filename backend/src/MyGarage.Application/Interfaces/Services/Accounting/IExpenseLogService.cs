using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IExpenseLogService : IGenericService<ExpenseLog>
    {
        List<ExpenseLogResponseDto> MapToDtos(IEnumerable<ExpenseLog> items);
        Task<ExpenseLogResponseDto?> GetDtoByIdAsync(long id);
        Task<ExpenseLogResponseDto> CreateDtoAsync(ExpenseLogCreateDto itemDto);
        Task<ExpenseLogResponseDto?> UpdateDtoAsync(long id, ExpenseLogUpdateDto itemDto);
    }
}
