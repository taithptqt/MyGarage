using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IGenericService<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<(IReadOnlyList<T> Items, int Total)> GetPageByIdAsync(int page, int pageSize);
        Task<T?> GetByIdAsync(object id);
        Task<T> AddAsync(T entity);
        Task<T?> UpdateAsync(object id, T entity);
        Task<bool> DeleteAsync(object id);
    }
}
