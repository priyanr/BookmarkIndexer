using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Shared.Exceptions;
using BookmarkIndexer.Shared.Types;
using BookmarkIndexer.Shared.Types.Bookmarks;
using Microsoft.EntityFrameworkCore;

namespace BookmarkIndexer.Core.Modules.Bookmarks;

public class BookmarkService(AppDbContext db, IBookmarkRepository bookmarks)
{
    const int PageSize = 50;

    public async Task<PagedResultDto<BookmarkDto>> List(IndexStatus? status, int page)
    {
        page = Math.Max(page, 1);
        var (items, total) = await bookmarks.ListByStatus(status, (page - 1) * PageSize, PageSize);
        return new PagedResultDto<BookmarkDto>(items, total, page, PageSize);
    }

    public async Task<BookmarkCountsDto> Counts()
    {
        var byStatus = await bookmarks.CountByStatus();
        return new BookmarkCountsDto(
            byStatus.Values.Sum(),
            byStatus.GetValueOrDefault(IndexStatus.Pending),
            byStatus.GetValueOrDefault(IndexStatus.Indexed),
            byStatus.GetValueOrDefault(IndexStatus.Failed));
    }

    public async Task<BookmarkContentDto> GetContent(int id)
    {
        var bookmark = await bookmarks.GetByIdOrNull(id) ?? throw new NotFoundException($"Bookmark {id} does not exist.");
        // A re-imported bookmark keeps its old content while Pending; it is shown again once indexing has replaced it.
        if (bookmark.Status != IndexStatus.Indexed)
        {
            throw new BadRequestException("Only indexed bookmarks have content.");
        }
        return new BookmarkContentDto(bookmark.Content ?? throw new InvalidOperationException($"Indexed bookmark {id} has no content."));
    }

    public async Task Retry(int id)
    {
        var bookmark = await bookmarks.GetByIdOrNull(id) ?? throw new NotFoundException($"Bookmark {id} does not exist.");
        // Indexed bookmarks already have chunks; retrying them would index the page twice.
        if (bookmark.Status != IndexStatus.Failed)
        {
            throw new BadRequestException("Only failed bookmarks can be retried.");
        }
        bookmark.Status = IndexStatus.Pending;
        bookmark.Error = null;
        await db.SaveChangesAsync();
    }

    public async Task RetryFailed()
    {
        await db.Bookmarks
            .Where(b => b.Status == IndexStatus.Failed)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, IndexStatus.Pending).SetProperty(b => b.Error, (string?)null));
    }
}
