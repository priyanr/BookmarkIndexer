using BookmarkIndexer.Shared.Types.Imports;

namespace BookmarkIndexer.Shared;

/// <summary>Reads one browser's bookmark export format. Only http(s) URLs are returned, each URL once.</summary>
public interface IBookmarkImporter
{
    List<ImportedBookmarkDto> Read(string content);
}
