using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Core.Model.Bookmarks;
using BookmarkIndexer.Core.Modules.Bookmarks;
using BookmarkIndexer.Shared.Types.Bookmarks;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Xunit;

namespace BookmarkIndexer.Tests.Search;

public class BookmarkSearchTests
{
    // Query "banana" with an embedding pointing mostly at Postgres (x) and a little at baking (y).
    // Vector ranks: Postgres 1, Banana 2, Garden 3. Full-text ranks: Banana 1 (the only one containing the word).
    // RRF with k = 60: Banana 1/62 + 1/61 = 0.0325, Postgres 1/61 = 0.0164, Garden 1/63 = 0.0159.
    [Fact]
    public async Task Ranks_keyword_and_vector_matches_with_reciprocal_rank_fusion()
    {
        await using var db = await TestDatabase.CreateEmpty();
        Add(db, "https://postgres.example/", ("Postgres full text search ranks documents with tsvector", Vector(x: 1)));
        Add(db, "https://banana.example/", ("Banana bread recipe with walnuts", Vector(y: 1)));
        Add(db, "https://garden.example/", ("Notes on spring gardening", Vector(z: 1)));
        await db.SaveChangesAsync();

        var results = await new BookmarkRepository(db).Search("banana", Vector(x: 0.9f, y: 0.3f), limit: 20);

        Assert.Equal(["https://banana.example/", "https://postgres.example/", "https://garden.example/"], results.Select(r => r.Url));
        Assert.Equal(1.0 / 62 + 1.0 / 61, results[0].Score, precision: 6);
        Assert.Equal("Banana bread recipe with walnuts", results[0].Snippet);
    }

    // Chunk 0 is nearest to the query vector (vector rank 1 → 1/61); chunk 1 is second by vector but the only
    // keyword match (1/62 + 1/61), so it is the chunk that represents the bookmark.
    [Fact]
    public async Task Returns_each_bookmark_once_with_its_best_chunk_as_snippet()
    {
        await using var db = await TestDatabase.CreateEmpty();
        Add(db, "https://recipes.example/",
            ("Sourdough starter needs daily feeding", Vector(x: 1)),
            ("Walnuts go into the banana bread last", Vector(y: 1)));
        await db.SaveChangesAsync();

        var result = Assert.Single(await new BookmarkRepository(db).Search("walnuts", Vector(x: 1, y: 0.5f), limit: 20));

        Assert.Equal("Walnuts go into the banana bread last", result.Snippet);
        Assert.Equal(1.0 / 62 + 1.0 / 61, result.Score, precision: 6);
    }

    [Fact]
    public async Task Only_returns_up_to_the_limit()
    {
        await using var db = await TestDatabase.CreateEmpty();
        Add(db, "https://a.example/", ("alpha", Vector(x: 1)));
        Add(db, "https://b.example/", ("beta", Vector(y: 1)));
        Add(db, "https://c.example/", ("gamma", Vector(z: 1)));
        await db.SaveChangesAsync();

        var results = await new BookmarkRepository(db).Search("nothing matches this", Vector(x: 1, y: 0.5f), limit: 2);

        Assert.Equal(["https://a.example/", "https://b.example/"], results.Select(r => r.Url));
    }

    // pgvector's HNSW index returns at most hnsw.ef_search rows (default 40), which would cut the 50 vector candidates short.
    [Fact]
    public async Task Takes_50_vector_candidates_when_the_hnsw_index_is_used()
    {
        await using var db = await TestDatabase.CreateEmpty();
        for (var i = 0; i < 60; i++)
        {
            Add(db, $"https://page{i:00}.example/", ($"page {i}", Vector(x: 1, y: i / 60f)));
        }
        await db.SaveChangesAsync();
        // With 60 rows Postgres would scan the table; forcing the index makes the test hit the same path as real data.
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("SET enable_seqscan = off");

        var results = await new BookmarkRepository(db).Search("nothing matches this", Vector(x: 1), limit: 100);

        Assert.Equal(50, results.Count);
    }

    [Fact]
    public async Task A_long_chunk_is_cut_to_300_characters_with_an_ellipsis()
    {
        await using var db = await TestDatabase.CreateEmpty();
        var text = string.Concat(Enumerable.Repeat("0123456789", 40));
        Add(db, "https://long.example/", (text, Vector(x: 1)));
        await db.SaveChangesAsync();

        var result = Assert.Single(await new BookmarkRepository(db).Search("anything", Vector(x: 1), limit: 20));

        Assert.Equal(text[..300] + "…", result.Snippet);
    }

    static void Add(AppDbContext db, string url, params (string Text, Vector Embedding)[] chunks)
    {
        var bookmark = new Bookmark { Url = url, Title = url, FolderPath = "Folder", Status = IndexStatus.Indexed };
        bookmark.Chunks.AddRange(chunks.Select((c, i) => new BookmarkChunk { Position = i, Text = c.Text, Embedding = c.Embedding }));
        db.Bookmarks.Add(bookmark);
    }

    static Vector Vector(float x = 0, float y = 0, float z = 0)
    {
        var values = new float[AppDbContext.EmbeddingDimensions];
        (values[0], values[1], values[2]) = (x, y, z);
        return new Vector(values);
    }
}
