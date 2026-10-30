namespace BookmarkIndexer.Shared.Types.Bookmarks;

public record BookmarkDto(
    int Id,
    string Url,
    string Title,
    string FolderPath,
    IndexStatus Status,
    string? Error,
    DateTimeOffset? AddedAt,
    DateTimeOffset? IndexedAt);
