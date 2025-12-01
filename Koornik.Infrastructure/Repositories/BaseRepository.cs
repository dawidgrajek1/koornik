using Koornik.Domain.Core.Models;
using Koornik.Domain.Core.Repositories;
using Koornik.Domain.Core.Specifications;
using Koornik.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Koornik.Infrastructure.Repositories;

public class BaseRepository<T> : IBaseRepository<T> where T : BaseEntity
{
    private readonly KoornikDbContext _dbContext;
    
    public BaseRepository(KoornikDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<T?> GetById(Guid id)
    {
        return await _dbContext.Set<T>().FindAsync(id);
    }

    public async Task<List<T>> ListAll()
    {
        return await _dbContext.Set<T>().ToListAsync();
    }

    public async Task<IEnumerable<T>> List(ISpecification<T> spec)
    {
        return await ApplySpecification(spec).ToListAsync();
    }

    public async Task<T?> FirstOrDefault(ISpecification<T> spec)
    {
        return await ApplySpecification(spec).FirstOrDefaultAsync();
    }

    public async Task<T> Add(T entity)
    {
        await _dbContext.Set<T>().AddAsync(entity);
        return entity;
    }

    public void Update(T entity)
    {
        _dbContext.Set<T>().Update(entity);
    }

    public void Delete(T entity)
    {
        _dbContext.Set<T>().Remove(entity);
    }

    public async Task<long> Count(ISpecification<T> spec)
    {
        return await ApplySpecification(spec).CountAsync();
    }
    
    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
    {
        return SpecificationEvaluator<T>.GetQuery(_dbContext.Set<T>().AsQueryable(), spec);
    }
}