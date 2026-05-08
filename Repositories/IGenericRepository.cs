namespace bestgen.Repositories;

public interface IGenericRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query();
    Task<TEntity?> FindAsync(int id);
    Task AddAsync(TEntity entity);
    void Remove(TEntity entity);
    Task SaveChangesAsync();
}
