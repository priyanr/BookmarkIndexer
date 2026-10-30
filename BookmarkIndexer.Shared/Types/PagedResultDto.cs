namespace BookmarkIndexer.Shared.Types;

public record PagedResultDto<T>(List<T> Items, int Total, int Page, int PageSize);
