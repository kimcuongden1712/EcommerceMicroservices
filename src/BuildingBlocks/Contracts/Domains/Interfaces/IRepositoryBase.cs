using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;

namespace Contracts.Domains.Interfaces;

public interface IRepositoryQueryBase<T, in K>
    where T : EntityBase<K>
{
    IQueryable<T> FindAll (bool trackChanges = false);
    IQueryable<T> FindAll (bool trackChanges = false, params Expression<Func<T, object>>[] includeProperties);
    IQueryable<T> FindByCondition (Expression<Func<T, bool>> expression, bool trackChanges = false);

    IQueryable<T> FindByCondition (Expression<Func<T, bool>> expression, bool trackChanges = false,
        params Expression<Func<T, object>>[] includeProperties);

    Task<T?> GetByIdAsync (K id, CancellationToken cancellationToken = default);
    Task<T?> GetByIdAsync (K id, CancellationToken cancellationToken = default, params Expression<Func<T, object>>[] includeProperties);
}

public interface IRepositoryQueryBase<T, K, TContext> : IRepositoryQueryBase<T, K> where T : EntityBase<K>
    where TContext : DbContext
{
}

public interface IRepositoryBase<T, K> : IRepositoryQueryBase<T, K>
    where T : EntityBase<K>
{
    void Create (T entity);
    Task<K> CreateAsync (T entity, CancellationToken cancellationToken = default);
    IList<K> CreateList (IEnumerable<T> entities);
    Task<IList<K>> CreateListAsync (IEnumerable<T> entities, CancellationToken cancellationToken = default);
    void Update (T entity);
    Task UpdateAsync (T entity, CancellationToken cancellationToken = default);
    void UpdateList (IEnumerable<T> entities);
    Task UpdateListAsync (IEnumerable<T> entities, CancellationToken cancellationToken = default);
    void Delete (T entity);
    Task DeleteAsync (T entity, CancellationToken cancellationToken = default);
    void DeleteList (IEnumerable<T> entities);
    Task DeleteListAsync (IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync (CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync (CancellationToken cancellationToken = default);
    Task EndTransactionAsync (CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync (CancellationToken cancellationToken = default);
}

public interface IRepositoryBase<T, K, TContext> : IRepositoryBase<T, K>
    where T : EntityBase<K>
    where TContext : DbContext
{
}
