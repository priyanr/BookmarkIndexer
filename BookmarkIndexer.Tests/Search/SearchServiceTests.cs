using BookmarkIndexer.Core.Modules.Bookmarks;
using BookmarkIndexer.Core.Modules.Search;
using BookmarkIndexer.Shared.Exceptions;
using Xunit;

namespace BookmarkIndexer.Tests.Search;

public class SearchServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_query_is_rejected(string? query)
    {
        await using var db = await TestDatabase.CreateEmpty();
        var embedder = new FakeEmbeddingGenerator(_ => new float[768]);

        var error = await Assert.ThrowsAsync<BadRequestException>(() => new SearchService(new BookmarkRepository(db), embedder).Search(query));

        Assert.Equal("Enter something to search for.", error.Message);
        Assert.Empty(embedder.Inputs);
    }

    [Fact]
    public async Task A_query_over_500_characters_is_rejected()
    {
        await using var db = await TestDatabase.CreateEmpty();

        var error = await Assert.ThrowsAsync<BadRequestException>(
            () => new SearchService(new BookmarkRepository(db), new FakeEmbeddingGenerator(_ => new float[768])).Search(new string('a', 501)));

        Assert.Equal("Search text can be at most 500 characters.", error.Message);
    }

    // nomic-embed-text expects queries prefixed with their task, matching the "search_document: " prefix used when indexing.
    [Fact]
    public async Task The_query_is_trimmed_and_embedded_with_the_search_query_prefix()
    {
        await using var db = await TestDatabase.CreateEmpty();
        var embedder = new FakeEmbeddingGenerator(_ => new float[768]);

        await new SearchService(new BookmarkRepository(db), embedder).Search("  banana  ");

        Assert.Equal(["search_query: banana"], embedder.Inputs);
    }
}
