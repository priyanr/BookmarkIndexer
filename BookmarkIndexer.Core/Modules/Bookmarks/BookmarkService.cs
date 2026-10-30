using BookmarkIndexer.Shared.Types;
using BookmarkIndexer.Shared.Types.Bookmarks;

namespace BookmarkIndexer.Core.Modules.Bookmarks;

public class BookmarkService(IBookmarkRepository bookmarks)
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
}
