using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Core.Model.Bookmarks;
using BookmarkIndexer.Core.Modules.Bookmarks;
using BookmarkIndexer.Shared.Exceptions;
using BookmarkIndexer.Shared.Types.Bookmarks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookmarkIndexer.Tests.Bookmarks;

public class BookmarkServiceTests
{
    [Fact]
    public async Task Retry_puts_a_failed_bookmark_back_to_pending_and_clears_the_error()
    {
        await using var db = await TestDatabase.CreateEmpty();
        var bookmark = Add(db, "https://flaky.example/", IndexStatus.Failed);
        await db.SaveChangesAsync();

        await Service(db).Retry(bookmark.Id);

        var reloaded = await db.Bookmarks.AsNoTracking().SingleAsync();
        Assert.Equal((IndexStatus.Pending, (string?)null), (reloaded.Status, reloaded.Error));
    }

    [Fact]
    public async Task Retry_refuses_a_bookmark_that_has_not_failed()
    {
        await using var db = await TestDatabase.CreateEmpty();
        var bookmark = Add(db, "https://fine.example/", IndexStatus.Indexed);
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<BadRequestException>(() => Service(db).Retry(bookmark.Id));

        Assert.Equal("Only failed bookmarks can be retried.", error.Message);
    }

    [Fact]
    public async Task Retry_of_an_unknown_bookmark_is_not_found()
    {
        await using var db = await TestDatabase.CreateEmpty();

        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).Retry(42));
    }

    [Fact]
    public async Task Retry_failed_puts_only_failed_bookmarks_back_to_pending()
    {
        await using var db = await TestDatabase.CreateEmpty();
        Add(db, "https://a.example/", IndexStatus.Failed);
        Add(db, "https://b.example/", IndexStatus.Failed);
        Add(db, "https://c.example/", IndexStatus.Indexed);
        await db.SaveChangesAsync();

        await Service(db).RetryFailed();

        var statuses = await db.Bookmarks.AsNoTracking().OrderBy(b => b.Url).Select(b => b.Status).ToListAsync();
        Assert.Equal([IndexStatus.Pending, IndexStatus.Pending, IndexStatus.Indexed], statuses);
    }

    static BookmarkService Service(AppDbContext db)
    {
        return new BookmarkService(db, new BookmarkRepository(db));
    }

    static Bookmark Add(AppDbContext db, string url, IndexStatus status)
    {
        var bookmark = new Bookmark { Url = url, Title = url, FolderPath = "", Status = status, Error = status == IndexStatus.Failed ? "HTTP 500" : null };
        db.Bookmarks.Add(bookmark);
        return bookmark;
    }
}
