using System.Linq.Expressions;

namespace SmkDoc.Application.Common.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task<(List<T> Items, int Total)> PagedListAsync<TKey>(
        Expression<Func<T, bool>>? predicate,
        Expression<Func<T, TKey>> orderBy,
        bool descending,
        int page,
        int limit,
        CancellationToken ct = default);
    Task<int> MaxOrDefaultAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, int>> selector, int defaultValue = 0, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
