using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Core.Model.Bookmarks;
using BookmarkIndexer.Core.Modules.Bookmarks;
using BookmarkIndexer.Core.Modules.Indexing;
using BookmarkIndexer.Shared;
using BookmarkIndexer.Shared.Types.Bookmarks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
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
    public async Task Stores_each_chunk_with_its_document_embedding()
    {
        await using var db = await TestDatabase.CreateEmpty();
        Seed(db, "https://article.example/");
        await db.SaveChangesAsync();
        var embedder = new FakeEmbeddingGenerator(_ => Vector(0.25f));

        await Service(db, new FakePageFetcher(_ => ArticleHtml), embedder).IndexNext(CancellationToken.None);

        var chunk = Assert.Single(await db.Chunks.AsNoTracking().ToListAsync());
        Assert.Equal(0, chunk.Position);
        Assert.Contains("Reciprocal rank fusion merges the two result lists", chunk.Text);
        Assert.Equal(Vector(0.25f), chunk.Embedding.ToArray());
        // nomic-embed-text expects documents and queries to be prefixed with their task.
        Assert.Equal(["search_document: " + chunk.Text], embedder.Inputs);
    }

    [Fact]
    public async Task A_failed_embedding_marks_the_bookmark_failed_and_stores_no_chunks()
    {
        await using var db = await TestDatabase.CreateEmpty();
        Seed(db, "https://article.example/");
        await db.SaveChangesAsync();
        var embedder = new FakeEmbeddingGenerator(_ => throw new HttpRequestException("Connection refused (localhost:11434)"));

        await Service(db, new FakePageFetcher(_ => ArticleHtml), embedder).IndexNext(CancellationToken.None);

        var bookmark = await db.Bookmarks.AsNoTracking().SingleAsync();
        Assert.Equal((IndexStatus.Failed, "Connection refused (localhost:11434)"), (bookmark.Status, bookmark.Error));
        Assert.Empty(await db.Chunks.ToListAsync());
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

    static IndexService Service(AppDbContext db, IPageFetcher fetcher, FakeEmbeddingGenerator? embedder = null)
    {
        embedder ??= new FakeEmbeddingGenerator(_ => Vector(1f));
        return new IndexService(db, new BookmarkRepository(db), fetcher, embedder, new FakeTimeProvider(Now), NullLogger<IndexService>.Instance);
    }

    static float[] Vector(float first)
    {
        var vector = new float[768];
        vector[0] = first;
        return vector;
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

    sealed class FakeEmbeddingGenerator(Func<string, float[]> embed) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public List<string> Inputs { get; } = [];

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            var inputs = values.ToList();
            Inputs.AddRange(inputs);
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(inputs.Select(v => new Embedding<float>(embed(v)))));
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        public void Dispose()
        {
        }
    }
}
