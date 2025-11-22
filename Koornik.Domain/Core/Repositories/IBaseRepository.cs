using Koornik.Domain.Core.Models;
using Koornik.Domain.Core.Specifications;

namespace Koornik.Domain.Core.Repositories;

#pragma warning disable CS0693 // Type parameter has the same name as the type parameter from outer type
public interface IBaseRepository<T> where T : BaseEntity
{
    Task<T> GetById<T>(Guid id);
    Task<IEnumerable<T>> ListAll<T>();
    Task<IEnumerable<T>> List<T>(ISpecification<T> spec);
    Task<T?> FirstOrDefault<T>(ISpecification<T?> spec);
    Task<T> Add<T>(T entity);
    Task Update<T>(T entity);
    Task Delete<T>(T entity);
    Task<ulong> Count<T>(ISpecification<T> spec);
}
#pragma warning restore CS0693 // Type parameter has the same name as the type parameter from outer type
