using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces.Services
{
    public interface IGenericService<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIdAsync(object id);
        Task<T> AddAsync(T entity);
        Task<T?> UpdateAsync(object id, T entity);
        Task<bool> DeleteAsync(object id);
    }
}

