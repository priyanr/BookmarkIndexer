using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Core.Model.Bookmarks;
using BookmarkIndexer.Core.Modules.Bookmarks;
using BookmarkIndexer.Core.Modules.Indexing;
using BookmarkIndexer.Shared;
using BookmarkIndexer.Shared.Types.Bookmarks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BookmarkIndexer.Tests.Indexing;

public class IndexServiceTests
{
    const string ArticleHtml = """
        <html><head><title>Hybrid search in Postgres</title></head>
        <body>
          <nav>Home | Blog | About</nav>
          <article>
            <h1>Hybrid search in Postgres</h1>
            <p>Hybrid search combines vector similarity from pgvector with the full-text ranking that Postgres has offered for years, so that both meaning and exact words count.</p>
            <p>Reciprocal rank fusion merges the two result lists without having to normalise their scores, which is why it is the usual choice when the scales differ.</p>
            <p>Each document is split into overlapping chunks, every chunk gets an embedding and a tsvector, and the best chunk per document decides its rank.</p>
          </article>
          <footer>Copyright 2026</footer>
        </body></html>
        """;

    static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Indexes_the_oldest_pending_bookmark_with_its_readable_text()
    {
        await using var db = await TestDatabase.CreateEmpty();
        Seed(db, "https://first.example/", "https://second.example/");
        await db.SaveChangesAsync();
        var fetcher = new FakePageFetcher(_ => ArticleHtml);

        var indexed = await Service(db, fetcher).IndexNext(CancellationToken.None);

        Assert.True(indexed);
        Assert.Equal(["https://first.example/"], fetcher.FetchedUrls);
        var first = await db.Bookmarks.AsNoTracking().SingleAsync(b => b.Url == "https://first.example/");
        Assert.Equal((IndexStatus.Indexed, 1, (DateTimeOffset?)Now, (string?)null), (first.Status, first.Attempts, first.IndexedAt, first.Error));
        Assert.Contains("Reciprocal rank fusion merges the two result lists", first.Content);
        Assert.DoesNotContain("Home | Blog | About", first.Content);
        Assert.Equal(IndexStatus.Pending, (await db.Bookmarks.AsNoTracking().SingleAsync(b => b.Url == "https://second.example/")).Status);
    }

    [Fact]
    public async Task A_page_that_fails_to_load_is_marked_failed_with_the_reason()
    {
        await using var db = await TestDatabase.CreateEmpty();
        Seed(db, "https://gone.example/");
        await db.SaveChangesAsync();

        await Service(db, new FakePageFetcher(_ => throw new HttpRequestException("HTTP 404 Not Found"))).IndexNext(CancellationToken.None);

        var bookmark = await db.Bookmarks.AsNoTracking().SingleAsync();
        Assert.Equal((IndexStatus.Failed, "HTTP 404 Not Found", 1, (string?)null), (bookmark.Status, bookmark.Error, bookmark.Attempts, bookmark.Content));
    }

    [Fact]
    public async Task A_page_without_readable_text_is_marked_failed()
    {
        await using var db = await TestDatabase.CreateEmpty();
        Seed(db, "https://empty.example/");
        await db.SaveChangesAsync();

        await Service(db, new FakePageFetcher(_ => "<html><body></body></html>")).IndexNext(CancellationToken.None);

        var bookmark = await db.Bookmarks.AsNoTracking().SingleAsync();
        Assert.Equal((IndexStatus.Failed, "The page has no readable text."), (bookmark.Status, bookmark.Error));
    }

    [Fact]
    public async Task Returns_false_when_nothing_is_pending()
    {
        await using var db = await TestDatabase.CreateEmpty();
        Seed(db, "https://done.example/");
        db.Bookmarks.Local.Single().Status = IndexStatus.Indexed;
        await db.SaveChangesAsync();
        var fetcher = new FakePageFetcher(_ => ArticleHtml);

        Assert.False(await Service(db, fetcher).IndexNext(CancellationToken.None));
        Assert.Empty(fetcher.FetchedUrls);
    }

    static IndexService Service(AppDbContext db, IPageFetcher fetcher)
    {
        return new IndexService(db, new BookmarkRepository(db), fetcher, new FakeTimeProvider(Now), NullLogger<IndexService>.Instance);
    }

    static void Seed(AppDbContext db, params string[] urls)
    {
        foreach (var url in urls)
        {
            db.Bookmarks.Add(new Bookmark { Url = url, Title = url, FolderPath = "", Status = IndexStatus.Pending });
        }
    }

    sealed class FakePageFetcher(Func<string, string> html) : IPageFetcher
    {
        public List<string> FetchedUrls { get; } = [];

        public Task<string> FetchHtml(string url, CancellationToken ct)
        {
            FetchedUrls.Add(url);
            return Task.FromResult(html(url));
        }
    }
}
