namespace BookmarkIndexer.Shared.Types.Search;

/// <summary>One bookmark matching a search. Snippet is its best-matching chunk, cut to 300 characters with "…"; Score is its reciprocal rank fusion score.</summary>
public record SearchResultDto(int BookmarkId, string Url, string Title, string FolderPath, string Snippet, double Score);
