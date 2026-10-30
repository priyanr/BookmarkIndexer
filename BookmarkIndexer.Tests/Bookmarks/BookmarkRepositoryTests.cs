using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Core.Model.Bookmarks;
using BookmarkIndexer.Core.Modules.Bookmarks;
using BookmarkIndexer.Shared.Types.Bookmarks;
using Xunit;

namespace BookmarkIndexer.Tests.Bookmarks;

public class BookmarkRepositoryTests
{
    [Fact]
    public async Task Lists_one_status_newest_first_with_the_total_before_paging()
    {
        await using var db = await TestDatabase.CreateEmpty();
        Seed(db, ("https://old.example/", IndexStatus.Pending, 2020), ("https://new.example/", IndexStatus.Pending, 2024),
            ("https://mid.example/", IndexStatus.Pending, 2022), ("https://done.example/", IndexStatus.Indexed, 2025));
        await db.SaveChangesAsync();

        var (items, total) = await new BookmarkRepository(db).ListByStatus(IndexStatus.Pending, skip: 0, take: 2);

        Assert.Equal(3, total);
        Assert.Equal(["https://new.example/", "https://mid.example/"], items.Select(b => b.Url));
    }

    [Fact]
    public async Task Counts_bookmarks_per_status()
    {
        await using var db = await TestDatabase.CreateEmpty();
        Seed(db, ("https://a.example/", IndexStatus.Pending, 2020), ("https://b.example/", IndexStatus.Pending, 2020),
            ("https://c.example/", IndexStatus.Failed, 2020));
        await db.SaveChangesAsync();

        var counts = await new BookmarkRepository(db).CountByStatus();

        Assert.Equal(new Dictionary<IndexStatus, int> { [IndexStatus.Pending] = 2, [IndexStatus.Failed] = 1 }, counts);
    }

    static void Seed(AppDbContext db, params (string Url, IndexStatus Status, int Year)[] bookmarks)
    {
        foreach (var (url, status, year) in bookmarks)
        {
            db.Bookmarks.Add(new Bookmark { Url = url, Title = url, FolderPath = "", Status = status, AddedAt = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero) });
        }
    }
}
