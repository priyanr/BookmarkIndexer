namespace BookmarkIndexer.Shared.Types.Imports;

/// <summary>Updated: an existing URL whose title or folder changed. Skipped: an existing URL with nothing to change.</summary>
public record ImportResultDto(int Added, int Updated, int Skipped);
