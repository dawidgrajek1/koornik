using Koornik.Domain.Core.Models;

namespace Koornik.Domain.Core.Repositories;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync();
    Task RollbackChangesAsync();
    IBaseRepository<T> Repository<T>() where T : BaseEntity;
}