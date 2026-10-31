using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Core.Model.Bookmarks;
using BookmarkIndexer.Core.Modules.Bookmarks;
using BookmarkIndexer.Shared;
using BookmarkIndexer.Shared.Types.Bookmarks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Pgvector;

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
    const string DocumentPrefix = "search_document: ";
    static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Indexes the next Pending bookmark. Returns false when there was nothing to do.</summary>
    public async Task<bool> IndexNext(CancellationToken ct)
    {
        // The lock on the bookmark lasts until the commit. If indexing is cancelled the rollback leaves it Pending.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var bookmark = await bookmarks.LockNextPendingOrNull(ct);
        if (bookmark == null)
        {
            return false;
        }

        bookmark.Attempts++;
        // A page that never answers must not hold up its index loop.
        using var timeout = new CancellationTokenSource(FetchTimeout, time);
        using var fetchToken = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            var html = await fetcher.FetchHtml(bookmark.Url, fetchToken.Token);
            var text = PageText.Extract(bookmark.Url, html);
            if (text.Length == 0)
            {
                MarkFailed("The page has no readable text.");
            }
            else
            {
                var chunks = TextChunks.Split(text);
                var embeddings = await embedder.GenerateAsync(chunks.Select(c => DocumentPrefix + c), cancellationToken: ct);
                bookmark.Chunks.Clear();
                bookmark.Chunks.AddRange(chunks.Select((chunkText, i) =>
                    new BookmarkChunk { Position = i, Text = chunkText, Embedding = new Vector(embeddings[i].Vector) }));
                bookmark.Content = text;
                bookmark.Status = IndexStatus.Indexed;
                bookmark.IndexedAt = time.GetUtcNow();
                bookmark.Error = null;
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Indexing bookmark {BookmarkId} {Url} timed out", bookmark.Id, bookmark.Url);
            MarkFailed($"The page did not load within {FetchTimeout.TotalSeconds:0} seconds.");
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Indexing bookmark {BookmarkId} {Url} failed", bookmark.Id, bookmark.Url);
            MarkFailed(e.Message);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;

        void MarkFailed(string error)
        {
            bookmark.Status = IndexStatus.Failed;
            bookmark.Error = error;
        }
    }
}
