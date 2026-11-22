using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Dsw2026Tpi.Data;

public class PersistenceEf : IPersistence
{
    private readonly Dsw2026TpiDbContext _context;

    public PersistenceEf(Dsw2026TpiDbContext context)
    {
        _context = context;
    }

    public async Task<List<T>> AddRange<T>(List<T> entity) where T : EntityBase
    {
        await _context.AddRangeAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<T> Add<T>(T entity) where T : EntityBase
    {
        await _context.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<T> Delete<T>(T entity) where T : EntityBase
    {
        entity.Delete();
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<T?> First<T>(Expression<Func<T, bool>> predicate, params string[] include) where T : EntityBase
    {
        return await Include(_context.Set<T>(), include).FirstOrDefaultAsync(predicate);
    }

    public async Task<IEnumerable<T>?> GetAll<T>(params string[] include) where T : EntityBase
    {
        return await Include(_context.Set<T>(), include).ToListAsync();
    }

    public async Task<T?> GetById<T>(Guid id, params string[] include) where T : EntityBase
    {
        return await Include(_context.Set<T>(), include).FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IEnumerable<T>?> GetFiltered<T>(Expression<Func<T, bool>> predicate, params string[] include) where T : EntityBase
    {
        return await Include(_context.Set<T>(), include).Where(predicate).ToListAsync();
    }

    public async Task<T> Update<T>(T entity) where T : EntityBase
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<List<T>> UpdateRange<T>(List<T> entities) where T : EntityBase
    {
        foreach (var entity in entities)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                _context.Update(entity);
            }
        }
        await _context.SaveChangesAsync();
        return entities;
    }

    public async Task<Pagination<T>> Paginate<T, TKey>(int pageSize, int pageIndex, Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> sortOrder, params string[] includes) where T : EntityBase
    {
        var normalizedPageSize = pageSize <= 0 ? 10 : pageSize;
        var normalizedPageIndex = pageIndex <= 0 ? 1 : pageIndex;
        var skip = (normalizedPageIndex - 1) * normalizedPageSize;

        var filtered = Include(_context.Set<T>(), includes)
                 .Where(predicate)
                 .OrderBy(sortOrder);

        var total = await filtered.CountAsync();

        var data = skip >= total
            ? []
            : await filtered.Skip(skip).Take(normalizedPageSize).ToListAsync();

        return new Pagination<T>(normalizedPageSize, normalizedPageIndex, data, total);
    }

    private static IQueryable<T> Include<T>(IQueryable<T> query, string[] includes) where T : EntityBase
    {
        var includedQuery = query;

        foreach (var include in includes)
        {
            includedQuery = includedQuery.Include(include);
        }
        return includedQuery;
    }
}
