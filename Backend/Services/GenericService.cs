using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class GenericService<T> : IGenericService<T> where T : class
    {
        protected readonly IGenericRepository<T> _repository;

        public GenericService(IGenericRepository<T> repository)
        {
            _repository = repository;
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public virtual async Task<T?> GetByIdAsync(object id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            return await _repository.AddAsync(entity);
        }

        public virtual async Task<T?> UpdateAsync(object id, T entity)
        {
            return await _repository.UpdateAsync(id, entity);
        }

        public virtual async Task<bool> DeleteAsync(object id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
