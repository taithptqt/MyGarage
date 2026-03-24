using Microsoft.EntityFrameworkCore;
using MyGarage.Data;

namespace MyGarage.Repositories
{
    public class GenericRepository<T> : Interfaces.IGenericRepository<T> where T : class
    {
        protected readonly GarageContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(GarageContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public virtual async Task<T?> GetByIdAsync(object id)
        {
            if (id is object[] compositeKey)
            {
                return await _dbSet.FindAsync(compositeKey);
            }
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public virtual async Task<T?> UpdateAsync(object id, T entity)
        {
            var existingEntity = id is object[] compositeKey ? await _dbSet.FindAsync(compositeKey) : await _dbSet.FindAsync(id);
            if (existingEntity == null) return null;

            _context.Entry(existingEntity).CurrentValues.SetValues(entity);
            await _context.SaveChangesAsync();
            return existingEntity;
        }

        public virtual async Task<bool> DeleteAsync(object id)
        {
            var entity = id is object[] compositeKey ? await _dbSet.FindAsync(compositeKey) : await _dbSet.FindAsync(id);
            if (entity == null) return false;

            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
