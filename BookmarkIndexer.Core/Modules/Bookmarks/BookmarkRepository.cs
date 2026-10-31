using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Core.Model.Bookmarks;
using BookmarkIndexer.Shared.Types.Bookmarks;
using BookmarkIndexer.Shared.Types.Search;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace BookmarkIndexer.Core.Modules.Bookmarks;

public interface IBookmarkRepository
{
    Task<Bookmark?> GetByIdOrNull(int id);
    Task<Bookmark?> LockNextPendingOrNull(CancellationToken ct);
    Task<(List<BookmarkDto> Items, int Total)> ListByStatus(IndexStatus? status, int skip, int take);
    Task<Dictionary<IndexStatus, int>> CountByStatus();
    Task<List<SearchResultDto>> Search(string query, Vector queryEmbedding, int limit);
}

public class BookmarkRepository(AppDbContext db) : IBookmarkRepository
{
    public async Task<Bookmark?> GetByIdOrNull(int id)
    {
        return await db.Bookmarks.FirstOrDefaultAsync(b => b.Id == id);
    }

    // Oldest first, so bookmarks are indexed in the order they were imported. Chunks are included because indexing replaces them.
    // Several index loops run at once: the row stays locked until the caller's transaction ends, and SKIP LOCKED lets
    // the other loops take the next bookmark instead of waiting. Call it inside a transaction.
    public async Task<Bookmark?> LockNextPendingOrNull(CancellationToken ct)
    {
        return await db.Bookmarks
            .FromSql($"""SELECT * FROM "Bookmarks" WHERE "Status" = 'Pending' ORDER BY "Id" LIMIT 1 FOR UPDATE SKIP LOCKED""")
            .Include(b => b.Chunks)
            .FirstOrDefaultAsync(ct);
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

    /// <summary>
    /// Hybrid search: the 50 chunks nearest to the query embedding and the 50 best full-text matches are merged
    /// with reciprocal rank fusion (k = 60), so neither score scale needs normalising. Each bookmark is ranked by its best chunk.
    /// </summary>
    public async Task<List<SearchResultDto>> Search(string query, Vector queryEmbedding, int limit)
    {
        // The HNSW index returns at most hnsw.ef_search rows (default 40); it must cover the 50 vector candidates.
        // SET LOCAL only lasts for the transaction, so the setting doesn't leak into pooled connections.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL hnsw.ef_search = 100");
        var results = await db.Database.SqlQuery<SearchResultDto>($"""
            WITH vector_hits AS (
                SELECT c."Id", row_number() OVER (ORDER BY c."Embedding" <=> {queryEmbedding}) AS rank
                FROM "Chunks" c
                ORDER BY c."Embedding" <=> {queryEmbedding}
                LIMIT 50
            ),
            text_hits AS (
                SELECT c."Id", row_number() OVER (ORDER BY ts_rank(c."SearchVector", q) DESC) AS rank
                FROM "Chunks" c, websearch_to_tsquery('english', {query}) q
                WHERE c."SearchVector" @@ q
                ORDER BY ts_rank(c."SearchVector", q) DESC
                LIMIT 50
            ),
            fused AS (
                SELECT COALESCE(v."Id", t."Id") AS chunk_id,
                       COALESCE(1.0 / (60 + v.rank), 0) + COALESCE(1.0 / (60 + t.rank), 0) AS score
                FROM vector_hits v FULL OUTER JOIN text_hits t ON v."Id" = t."Id"
            ),
            best_per_bookmark AS (
                SELECT DISTINCT ON (c."BookmarkId") c."BookmarkId", c."Text", f.score
                FROM fused f JOIN "Chunks" c ON c."Id" = f.chunk_id
                ORDER BY c."BookmarkId", f.score DESC
            )
            SELECT b."Id" AS "BookmarkId", b."Url", b."Title", b."FolderPath",
                   CASE WHEN length(best."Text") > 300 THEN left(best."Text", 300) || '…' ELSE best."Text" END AS "Snippet",
                   best.score::float8 AS "Score"
            FROM best_per_bookmark best JOIN "Bookmarks" b ON b."Id" = best."BookmarkId"
            ORDER BY best.score DESC, b."Id"
            LIMIT {limit}
            """).ToListAsync();
        await transaction.CommitAsync();
        return results;
    }
}
