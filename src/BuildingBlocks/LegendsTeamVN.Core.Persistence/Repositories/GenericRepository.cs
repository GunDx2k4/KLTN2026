using System.Linq.Expressions;
using LegendsTeamVN.Core.Domain.Aggregates;
using LegendsTeamVN.Core.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.DbContexts;

using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.Core.Persistence.Repositories;


public abstract class GenericRepository<TDbContext, TEntity, TKey>(TDbContext dbContext) : IGenericRepository<TEntity, TKey>
    where TDbContext : DbContextUnitOfWork<TDbContext>
    where TEntity : AggregateRoot<TKey>
{
    protected readonly TDbContext DbContext = dbContext;

    protected DbSet<TEntity> DbSet => DbContext.Set<TEntity>();

    public IUnitOfWork UnitOfWork => DbContext;

    public async Task<TEntity?> FindByIdAsync(TKey id, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] includeProperties)
    {
        return await FindAll(null, includeProperties).AsTracking().SingleOrDefaultAsync(x => x.Id!.Equals(id), cancellationToken);
    }

    public async Task<TEntity?> FindSingleAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] includeProperties)
    {
        return await FindAll(predicate, includeProperties).AsTracking().SingleOrDefaultAsync(cancellationToken);
    }

    public IQueryable<TEntity> FindAll(Expression<Func<TEntity, bool>>? predicate = null, params Expression<Func<TEntity, object>>[] includeProperties)
    {
        IQueryable<TEntity> items = DbSet.AsNoTracking();
        if (includeProperties != null)
            foreach (var includeProperty in includeProperties)
                items = items.Include(includeProperty);

        if (predicate is not null)
            items = items.Where(predicate);

        return items;
    }

    public void Add(TEntity entity)
    {
        DbSet.Add(entity);
    }

    public void Update(TEntity entity)
    {
        DbSet.Update(entity);
    }

    public void Remove(TEntity entity)
    {
        DbSet.Remove(entity);
    }

    public void RemoveMultiple(IEnumerable<TEntity> entities)
    {
        DbSet.RemoveRange(entities);
    }
}
