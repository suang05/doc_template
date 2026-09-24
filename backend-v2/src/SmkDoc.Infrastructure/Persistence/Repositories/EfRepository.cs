using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public class EfRepository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public EfRepository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, ct);
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, ct);
    }

    public virtual async Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        return predicate != null
            ? await _dbSet.Where(predicate).ToListAsync(ct)
            : await _dbSet.ToListAsync(ct);
    }

    public virtual async Task<int> MaxOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, int>> selector,
        int defaultValue = 0,
        CancellationToken ct = default)
    {
        var query = _dbSet.Where(predicate);
        if (!await query.AnyAsync(ct))
            return defaultValue;

        return await query.MaxAsync(selector, ct);
    }

    public virtual async Task<(List<T> Items, int Total)> PagedListAsync<TKey>(
        Expression<Func<T, bool>>? predicate,
        Expression<Func<T, TKey>> orderBy,
        bool descending,
        int page,
        int limit,
        CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet;
        if (predicate != null) query = query.Where(predicate);

        int total = await query.CountAsync(ct);

        IQueryable<T> ordered = descending
            ? query.OrderByDescending(orderBy)
            : query.OrderBy(orderBy);

        var items = await ordered
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(ct);

        return (items, total);
    }

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
    }

    public virtual void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public virtual void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }
}
