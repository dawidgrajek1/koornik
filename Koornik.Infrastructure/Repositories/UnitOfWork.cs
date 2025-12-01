using Koornik.Domain.Core.Models;
using Koornik.Domain.Core.Repositories;
using Koornik.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Koornik.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _dbContext;
    private readonly IDictionary<Type, dynamic> _repositories;

    public UnitOfWork(KoornikDbContext dbContext)
    {
        _dbContext = dbContext;
        _repositories = new Dictionary<Type, dynamic>();
    }

    public IBaseRepository<T> Repository<T>() where T : BaseEntity
    {
        var entityType = typeof(T);
        if (_repositories.TryGetValue(entityType, out var repository))
        {
            return repository;
        }
        
        var repositoryType = typeof(BaseRepository<>);
        var newRepository = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(T)), _dbContext);

        if (newRepository is null)
        {
            throw new InvalidOperationException($"Could not create repository for type {entityType.Name}");
        }
        
        _repositories.Add(entityType, newRepository);
        
        return (IBaseRepository<T>)newRepository;
    }
    
    public async Task<int> SaveChangesAsync()
    {
        return await _dbContext.SaveChangesAsync();
    }

    public async Task RollbackChangesAsync()
    {
        await _dbContext.Database.RollbackTransactionAsync();
    }
}