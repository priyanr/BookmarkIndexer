using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Core.Model.Bookmarks;
using BookmarkIndexer.Shared.Types.Bookmarks;
using Microsoft.EntityFrameworkCore;

namespace BookmarkIndexer.Core.Modules.Bookmarks;

public interface IBookmarkRepository
{
    Task<Bookmark?> GetNextPendingOrNull(CancellationToken ct);
    Task<(List<BookmarkDto> Items, int Total)> ListByStatus(IndexStatus? status, int skip, int take);
    Task<Dictionary<IndexStatus, int>> CountByStatus();
}

public class BookmarkRepository(AppDbContext db) : IBookmarkRepository
{
    // Oldest first, so bookmarks are indexed in the order they were imported.
    public async Task<Bookmark?> GetNextPendingOrNull(CancellationToken ct)
    {
        return await db.Bookmarks.Where(b => b.Status == IndexStatus.Pending).OrderBy(b => b.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>Newest bookmarks first; a null status lists every bookmark.</summary>
    public async Task<(List<BookmarkDto> Items, int Total)> ListByStatus(IndexStatus? status, int skip, int take)
    {
        var query = db.Bookmarks.AsNoTracking();
        if (status != null)
        {
            query = query.Where(b => b.Status == status);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.AddedAt).ThenByDescending(b => b.Id)
            .Skip(skip).Take(take)
            .Select(b => new BookmarkDto(b.Id, b.Url, b.Title, b.FolderPath, b.Status, b.Error, b.AddedAt, b.IndexedAt))
            .ToListAsync();
        return (items, total);
    }

    public async Task<Dictionary<IndexStatus, int>> CountByStatus()
    {
        return await db.Bookmarks
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count);
    }
}
