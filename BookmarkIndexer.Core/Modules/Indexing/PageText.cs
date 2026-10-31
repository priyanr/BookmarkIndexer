using AngleSharp.Html.Parser;
using SmartReader;

namespace BookmarkIndexer.Core.Modules.Indexing;

/// <summary>The text of a page worth indexing: the readable article, or the whole body when the article extraction missed the content.</summary>
public static class PageText
{
    const int ShortArticleLength = 500;

    public static string Extract(string url, string html)
    {
        // Readability keeps the article and drops navigation, ads and footers, so search matches what the page is about.
        var article = new Reader(url, html).GetArticle().TextContent?.Trim() ?? "";
        if (article.Length >= ShortArticleLength)
        {
            return article;
        }

        // On some sites (docs portals, list pages) Readability keeps only a banner or nothing at all. When the page
        // holds far more text than it found, the whole body is the better thing to index.
        var document = new HtmlParser().ParseDocument(html);
        foreach (var element in document.QuerySelectorAll("script, style, noscript, template").ToList())
        {
            element.Remove();
        }
        var body = string.Join(' ', (document.Body?.TextContent ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return body.Length > 3 * article.Length ? body : article;
    }
}
