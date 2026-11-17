using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class GenericRepository<T> : Interfaces.IGenericRepository<T> where T : class
    {
        protected readonly IGarageContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(IGarageContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public virtual async Task<(IReadOnlyList<T> Items, int Total)> GetPageByIdAsync(int page, int pageSize)
        {
            return await GetOrderedPageAsync(
                _dbSet.AsNoTracking().OrderBy(x => EF.Property<long>(x, "Id")), page, pageSize);
        }

        protected static async Task<(IReadOnlyList<T> Items, int Total)> GetOrderedPageAsync(
            IOrderedQueryable<T> query, int page, int pageSize)
        {
            var total = await query.CountAsync();
            var offset = ((long)page - 1) * pageSize;
            if (offset >= total) return (new List<T>(), total);
            var items = await query.Skip((int)offset).Take(pageSize).ToListAsync();
            return (items, total);
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

