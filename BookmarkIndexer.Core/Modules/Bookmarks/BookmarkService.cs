using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Shared.Types;
using BookmarkIndexer.Shared.Types.Bookmarks;
using Microsoft.EntityFrameworkCore;

namespace BookmarkIndexer.Core.Modules.Bookmarks;

public class BookmarkService(AppDbContext db)
{
    const int PageSize = 50;

    public async Task<PagedResultDto<BookmarkDto>> List(IndexStatus? status, int page)
    {
        page = Math.Max(page, 1);
        var query = db.Bookmarks.AsNoTracking();
        if (status != null)
        {
            query = query.Where(b => b.Status == status);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.AddedAt).ThenByDescending(b => b.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(b => new BookmarkDto(b.Id, b.Url, b.Title, b.FolderPath, b.Status, b.Error, b.AddedAt, b.IndexedAt))
            .ToListAsync();
        return new PagedResultDto<BookmarkDto>(items, total, page, PageSize);
    }

    public async Task<BookmarkCountsDto> Counts()
    {
        var byStatus = await db.Bookmarks
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count);
        return new BookmarkCountsDto(
            byStatus.Values.Sum(),
            byStatus.GetValueOrDefault(IndexStatus.Pending),
            byStatus.GetValueOrDefault(IndexStatus.Indexed),
            byStatus.GetValueOrDefault(IndexStatus.Failed));
    }
}
