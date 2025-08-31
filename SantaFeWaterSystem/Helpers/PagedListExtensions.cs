using Microsoft.EntityFrameworkCore;
using X.PagedList;
using System.Linq;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Helpers
{
    public static class PagedListExtensions
    {
        public static async Task<StaticPagedList<T>> ToPagedListAsync<T>(this IQueryable<T> source, int pageNumber, int pageSize)
        {
            var count = await source.CountAsync();
            var items = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
            return new StaticPagedList<T>(items, pageNumber, pageSize, count);
        }
    }
}
