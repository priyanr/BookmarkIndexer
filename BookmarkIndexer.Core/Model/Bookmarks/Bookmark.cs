using BookmarkIndexer.Shared.Types.Bookmarks;

namespace BookmarkIndexer.Core.Model.Bookmarks;

public class Bookmark
{
    public int Id { get; set; }
    public required string Url { get; set; }
    public required string Title { get; set; }
    public required string FolderPath { get; set; }
    public DateTimeOffset? AddedAt { get; set; }
    public IndexStatus Status { get; set; }
    public string? Error { get; set; }
    public string? Content { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? IndexedAt { get; set; }
    public List<BookmarkChunk> Chunks { get; set; } = [];
}
