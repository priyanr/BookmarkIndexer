namespace BookmarkIndexer.Shared;

/// <summary>Loads a URL in a browser and returns the rendered HTML. Throws when the page can't be loaded (including HTTP errors).</summary>
public interface IPageFetcher
{
    Task<string> FetchHtml(string url, CancellationToken ct);
}
