using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Core.Model.Bookmarks;
using BookmarkIndexer.Core.Modules.Bookmarks;
using BookmarkIndexer.Shared;
using BookmarkIndexer.Shared.Types.Bookmarks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Pgvector;
using SmartReader;

namespace BookmarkIndexer.Core.Modules.Indexing;

public class IndexService(
    AppDbContext db,
    IBookmarkRepository bookmarks,
    IPageFetcher fetcher,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    TimeProvider time,
    ILogger<IndexService> logger)
{
    // nomic-embed-text is trained with task prefixes; documents and queries must use the matching one.
    public const string DocumentPrefix = "search_document: ";

    /// <summary>Indexes the next Pending bookmark. Returns false when there was nothing to do.</summary>
    public async Task<bool> IndexNext(CancellationToken ct)
    {
        var bookmark = await bookmarks.GetNextPendingOrNull(ct);
        if (bookmark == null)
        {
            return false;
        }

        bookmark.Attempts++;
        try
        {
            var html = await fetcher.FetchHtml(bookmark.Url, ct);
            // Readability keeps the article and drops navigation, ads and footers, so search matches what the page is about.
            var text = new Reader(bookmark.Url, html).GetArticle().TextContent?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                MarkFailed("The page has no readable text.");
            }
            else
            {
                var chunks = TextChunks.Split(text);
                var embeddings = await embedder.GenerateAsync(chunks.Select(c => DocumentPrefix + c), cancellationToken: ct);
                bookmark.Chunks.AddRange(chunks.Select((chunkText, i) =>
                    new BookmarkChunk { Position = i, Text = chunkText, Embedding = new Vector(embeddings[i].Vector) }));
                bookmark.Content = text;
                bookmark.Status = IndexStatus.Indexed;
                bookmark.IndexedAt = time.GetUtcNow();
                bookmark.Error = null;
            }
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Indexing bookmark {BookmarkId} {Url} failed", bookmark.Id, bookmark.Url);
            MarkFailed(e.Message);
        }

        await db.SaveChangesAsync(ct);
        return true;

        void MarkFailed(string error)
        {
            bookmark.Status = IndexStatus.Failed;
            bookmark.Error = error;
        }
    }
}
