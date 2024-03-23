using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LinkDotNet.Blog.Infrastructure.Persistence.InMemory;

public static class PaginatedListQueryExtensions
{
    public static Task<IPagedList<T>> ToPagedList<T>(this IEnumerable<T> source, int pageIndex, int pageSize)
    {
#pragma warning disable CA1851
        var count = source.Count();

        if (count > 0)
        {
            var items = source
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToArray();
            return Task.FromResult<IPagedList<T>>(new PagedList<T>(items, count, pageIndex, pageSize));
        }

        return Task.FromResult<IPagedList<T>>(PagedList<T>.Empty);
    }
#pragma warning restore CA1851
}
