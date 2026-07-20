using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Repositories.Interfaces;
using System.Linq.Expressions;

namespace NUH_PORTAL.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<T> _set;

        public Repository(AppDbContext context)
        {
            _context = context;
            _set = context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);
        public async Task<List<T>> GetAllAsync() => await _set.ToListAsync();
        public IQueryable<T> Query() => _set.AsQueryable();
        public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate) => await _set.AnyAsync(predicate);

        public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
            => predicate == null ? await _set.CountAsync() : await _set.CountAsync(predicate);

        public async Task<T?> FindAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> q = _set;
            foreach (var inc in includes) q = q.Include(inc);
            return await q.FirstOrDefaultAsync(predicate);
        }

        public async Task<List<T>> FindAllAsync(Expression<Func<T, bool>>? predicate = null, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> q = _set;
            foreach (var inc in includes) q = q.Include(inc);
            if (predicate != null) q = q.Where(predicate);
            return await q.ToListAsync();
        }

        public async Task AddAsync(T entity) => await _set.AddAsync(entity);
        public async Task AddRangeAsync(IEnumerable<T> entities) => await _set.AddRangeAsync(entities);
        public void Update(T entity) => _set.Update(entity);
        public void Remove(T entity) => _set.Remove(entity);
    }
}
