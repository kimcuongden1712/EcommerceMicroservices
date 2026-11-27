using Microsoft.EntityFrameworkCore;

namespace Contracts.Domains.Interfaces;

public interface IUnitOfWork : IDisposable
{
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
{
}
