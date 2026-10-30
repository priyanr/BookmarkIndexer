using BookmarkIndexer.Core.Modules.Bookmarks;
using BookmarkIndexer.Shared.Exceptions;
using BookmarkIndexer.Shared.Types.Search;
using Microsoft.Extensions.AI;
using Pgvector;

namespace BookmarkIndexer.Core.Modules.Search;

public class SearchService(IBookmarkRepository bookmarks, IEmbeddingGenerator<string, Embedding<float>> embedder)
{
    // nomic-embed-text pairs this with the "search_document: " prefix used when indexing.
    const string QueryPrefix = "search_query: ";
    const int MaxQueryLength = 500;
    const int ResultCount = 20;

    public async Task<List<SearchResultDto>> Search(string? query)
    {
        query = query?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            throw new BadRequestException("Enter something to search for.");
        }
        if (query.Length > MaxQueryLength)
        {
            throw new BadRequestException($"Search text can be at most {MaxQueryLength} characters.");
        }

        var embedding = await embedder.GenerateVectorAsync(QueryPrefix + query);
        return await bookmarks.Search(query, new Vector(embedding), ResultCount);
    }
}
