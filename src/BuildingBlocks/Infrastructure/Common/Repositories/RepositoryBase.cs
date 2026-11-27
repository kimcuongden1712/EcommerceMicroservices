using Contracts.Domains;
using Contracts.Domains.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Common;

public class RepositoryBase<T, K> : RepositoryQueryBase<T, K> where T : EntityBase<K>
{
}

public class RepositoryBase<T, K, TContext> : RepositoryQueryBase<T, K, TContext>,
    IRepositoryBase<T, K, TContext>
    where T : EntityBase<K>
    where TContext : DbContext
{
    private readonly TContext _dbContext;
    private readonly IUnitOfWork<TContext> _unitOfWork;

    public RepositoryBase (TContext dbContext, IUnitOfWork<TContext> unitOfWork) : base(dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public Task<IDbContextTransaction> BeginTransactionAsync (CancellationToken cancellationToken = default)
    {
        return _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task EndTransactionAsync (CancellationToken cancellationToken = default)
    {
        await SaveChangesAsync(cancellationToken);
        await _dbContext.Database.CommitTransactionAsync(cancellationToken);
    }

    public Task RollbackTransactionAsync (CancellationToken cancellationToken = default)
    {
        return _dbContext.Database.RollbackTransactionAsync(cancellationToken);
    }

    public void Create (T entity)
    {
        _dbContext.Set<T>().Add(entity);
    }

    public async Task<K> CreateAsync (T entity, CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<T>().AddAsync(entity, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public IList<K> CreateList (IEnumerable<T> entities)
    {
        _dbContext.Set<T>().AddRange(entities);
        return entities.Select(x => x.Id).ToList();
    }

    public async Task<IList<K>> CreateListAsync (IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<T>().AddRangeAsync(entities, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return entities.Select(x => x.Id).ToList();
    }

    public void Update (T entity)
    {
        if (_dbContext.Entry(entity).State == EntityState.Unchanged)
            return;

        var exist = _dbContext.Set<T>().Find(entity.Id);
        _dbContext.Entry(exist).CurrentValues.SetValues(entity);
    }

    public async Task UpdateAsync (T entity, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Entry(entity).State == EntityState.Unchanged)
            return;

        var exist = await _dbContext.Set<T>().FindAsync(new object?[] { entity.Id }, cancellationToken);
        _dbContext.Entry(exist).CurrentValues.SetValues(entity);
        await SaveChangesAsync(cancellationToken);
    }

    public void UpdateList (IEnumerable<T> entities)
    {
        foreach (var entity in entities)
            Update(entity);
    }

    public async Task UpdateListAsync (IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
            Update(entity);
        await SaveChangesAsync(cancellationToken);
    }

    public void Delete (T entity)
    {
        _dbContext.Set<T>().Remove(entity);
    }

    public async Task DeleteAsync (T entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<T>().Remove(entity);
        await SaveChangesAsync(cancellationToken);
    }

    public void DeleteList (IEnumerable<T> entities)
    {
        _dbContext.Set<T>().RemoveRange(entities);
    }

    public async Task DeleteListAsync (IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<T>().RemoveRange(entities);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync (CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Detaches a tracked entity from the DbContext to prevent it from being updated or deleted.
    /// </summary>
    /// <param name="id"></param>
    protected void DetachTrackedEntity (K id)
    {
        var tracked = _dbContext.ChangeTracker.Entries<T>()
            .SingleOrDefault(entry => entry.Entity.Id.Equals(id));

        if (tracked is not null)
            tracked.State = EntityState.Detached;
    }
}
