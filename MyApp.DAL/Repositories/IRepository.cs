using System.Linq.Expressions;
using MyApp.Core.Entities;

namespace MyApp.DAL.Repositories;

public interface IRepository<T> where T : BaseEntity
{
    IQueryable<T> Query(bool tracking = false);
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
