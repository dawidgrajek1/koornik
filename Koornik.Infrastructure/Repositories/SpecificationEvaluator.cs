using Koornik.Domain.Core.Models;
using Koornik.Domain.Core.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Koornik.Infrastructure.Repositories;

public static class SpecificationEvaluator<T> where T : BaseEntity
{
    public static IQueryable<T> GetQuery(IQueryable<T> inputQuery, ISpecification<T> spec)
    {
        var query = inputQuery;

        // modify the IQueryable using the specification's criteria expression
        if (spec.Criteria is not null)
        {
            query = query.Where(spec.Criteria);
        }

        // Includes all expression-based includes
        query = spec.Includes.Aggregate(query,
            (current, include) => current.Include(include));

        // Apply ordering if expressions are set
        if (spec.OrderBy is not null)
        {
            query = query.OrderBy(spec.OrderBy);
        }
        else if (spec.OrderByDescending is not null)
        {
            query = query.OrderByDescending(spec.OrderByDescending);
        }

        // Apply paging if enabled
        if (spec.IsPagingEnabled)
        {
            query = query.Skip(spec.Skip).Take(spec.Take);
        }

        return query;
    }
}