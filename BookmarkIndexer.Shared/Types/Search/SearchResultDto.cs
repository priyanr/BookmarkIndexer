namespace BookmarkIndexer.Shared.Types.Search;

/// <summary>One bookmark matching a search. Snippet is the start of its best-matching chunk; Score is its reciprocal rank fusion score.</summary>
public record SearchResultDto(int BookmarkId, string Url, string Title, string FolderPath, string Snippet, double Score);
