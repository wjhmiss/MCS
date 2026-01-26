using MCS.Core.Entities;
using SqlSugar;
using System.Linq.Expressions;

namespace MCS.Core.Repositories
{
    public interface IRepository<T> where T : class, new()
    {
        Task<T?> GetByIdAsync(int id);
        Task<List<T>> GetAllAsync();
        Task<List<T>> QueryAsync(Expression<Func<T, bool>> expression);
        Task<int> InsertAsync(T entity);
        Task<int> InsertRangeAsync(List<T> entities);
        Task<bool> UpdateAsync(T entity);
        Task<bool> DeleteAsync(int id);
        Task<bool> DeleteAsync(T entity);
        Task<int> DeleteRangeAsync(List<int> ids);
    }

    public class Repository<T> : IRepository<T> where T : class, new()
    {
        protected readonly ISqlSugarClient _db;

        public Repository(ISqlSugarClient db)
        {
            _db = db;
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _db.Queryable<T>().InSingleAsync(id);
        }

        public async Task<List<T>> GetAllAsync()
        {
            return await _db.Queryable<T>().ToListAsync();
        }

        public async Task<List<T>> QueryAsync(System.Linq.Expressions.Expression<Func<T, bool>> expression)
        {
            return await _db.Queryable<T>().Where(expression).ToListAsync();
        }

        public async Task<int> InsertAsync(T entity)
        {
            return await _db.Insertable(entity).ExecuteReturnIdentityAsync();
        }

        public async Task<int> InsertRangeAsync(List<T> entities)
        {
            return await _db.Insertable(entities).ExecuteCommandAsync();
        }

        public async Task<bool> UpdateAsync(T entity)
        {
            return await _db.Updateable(entity).ExecuteCommandHasChangeAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _db.Deleteable<T>().In(id).ExecuteCommandHasChangeAsync();
        }

        public async Task<bool> DeleteAsync(T entity)
        {
            return await _db.Deleteable(entity).ExecuteCommandHasChangeAsync();
        }

        public async Task<int> DeleteRangeAsync(List<int> ids)
        {
            return await _db.Deleteable<T>().In(ids).ExecuteCommandAsync();
        }
    }
}
