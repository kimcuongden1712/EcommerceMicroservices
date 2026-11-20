using Infrastructure.Extensions;
using Product.API.Entities;
using System.Linq.Dynamic.Core;

namespace Product.API.Extensions;

public static class RepositoryProductExtensions
{
    //public static IQueryable<Products> FilterProducts (this IQueryable<Products> queryable, decimal minPrice, decimal maxPrice) =>
    //    queryable.Where(e => (e.Price >= minPrice && e.Price <= maxPrice));

    public static IQueryable<Products> SearchTerm (this IQueryable<Products> queryable, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return queryable;
        var lowerCaseTerm = searchTerm.Trim().ToLower();
        return queryable.Where(e => e.Name.ToLower()
            .Contains(lowerCaseTerm));
    }

    public static IQueryable<Products> SortBy (this IQueryable<Products> queryable, string? orderByQueryString)
    {
        if (string.IsNullOrWhiteSpace(orderByQueryString))
            return queryable.OrderBy(e => e.Name);

        var orderQuery = OrderQueryBuilder.CreateOrderQuery<Products>(orderByQueryString);
        if (string.IsNullOrWhiteSpace(orderQuery))
            return queryable.OrderBy(e => e.Name);

        return queryable.OrderBy(orderQuery);
    }
}