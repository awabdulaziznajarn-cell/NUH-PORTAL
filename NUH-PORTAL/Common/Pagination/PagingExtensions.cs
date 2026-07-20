using Microsoft.EntityFrameworkCore;

namespace NUH_PORTAL.Common.Pagination
{
    public static class PagingExtensions
    {
        public static async Task<QueryResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, QueryParams p)
        {
            var page = p.Page < 1 ? 1 : p.Page;
            var size = (p.PageSize < 1 || p.PageSize > 200) ? 20 : p.PageSize;
            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * size).Take(size).ToListAsync();
            return new QueryResult<T> { Items = items, TotalCount = total, Page = page, PageSize = size };
        }
    }
}
