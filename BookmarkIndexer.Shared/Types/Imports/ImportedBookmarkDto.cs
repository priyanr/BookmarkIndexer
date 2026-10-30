namespace BookmarkIndexer.Shared.Types.Imports;

public record ImportedBookmarkDto(string Url, string Title, string FolderPath, DateTimeOffset? AddedAt);
