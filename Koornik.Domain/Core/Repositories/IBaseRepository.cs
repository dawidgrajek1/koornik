using Koornik.Domain.Core.Models;
using Koornik.Domain.Core.Specifications;

namespace Koornik.Domain.Core.Repositories;

public interface IBaseRepository<T> where T : BaseEntity
{
    Task<T?> GetById(Guid id);
    Task<List<T>> ListAll();
    Task<IEnumerable<T>> List(ISpecification<T> spec);
    Task<T?> FirstOrDefault(ISpecification<T> spec);
    Task<T> Add(T entity);
    void Update(T entity);
    void Delete(T entity);
    Task<long> Count(ISpecification<T> spec);
}
