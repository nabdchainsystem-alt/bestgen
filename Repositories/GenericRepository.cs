using bestgen.Data;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Repositories;

public class GenericRepository<TEntity> : IGenericRepository<TEntity> where TEntity : class
{
    private readonly ApplicationDbContext _context;

    public GenericRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public IQueryable<TEntity> Query() => _context.Set<TEntity>();

    public Task<TEntity?> FindAsync(int id) => _context.Set<TEntity>().FindAsync(id).AsTask();

    public async Task AddAsync(TEntity entity) => await _context.Set<TEntity>().AddAsync(entity);

    public void Remove(TEntity entity) => _context.Set<TEntity>().Remove(entity);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
