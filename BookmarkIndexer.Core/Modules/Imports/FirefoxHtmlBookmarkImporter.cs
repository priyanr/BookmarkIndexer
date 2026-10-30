using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using BookmarkIndexer.Shared;
using BookmarkIndexer.Shared.Types.Imports;

namespace BookmarkIndexer.Core.Modules.Imports;

/// <summary>Reads the Netscape bookmark HTML that Firefox writes with "Export Bookmarks to HTML".</summary>
public class FirefoxHtmlBookmarkImporter : IBookmarkImporter
{
    public List<ImportedBookmarkDto> Read(string content)
    {
        var document = new HtmlParser().ParseDocument(content);
        var bookmarks = new List<ImportedBookmarkDto>();
        var seenUrls = new HashSet<string>();

        foreach (var link in document.QuerySelectorAll("a[href]"))
        {
            var url = link.GetAttribute("href")!.Trim();
            // place:, javascript: and file: entries are Firefox smart folders, bookmarklets and local files; they can't be fetched.
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!seenUrls.Add(url))
            {
                continue;
            }
            bookmarks.Add(new ImportedBookmarkDto(url, link.TextContent.Trim(), FolderPath(link), AddedAt(link)));
        }
        return bookmarks;
    }

    // A folder is an <H3> followed by the <DL> holding its contents, so each enclosing <DL> names one folder level.
    static string FolderPath(IElement link)
    {
        var folders = link.Ancestors<IElement>()
            .Where(e => e.LocalName == "dl")
            .Select(dl => dl.PreviousElementSibling)
            .Where(heading => heading?.LocalName == "h3")
            .Select(heading => heading!.TextContent.Trim())
            .Reverse();
        return string.Join("/", folders);
    }

    static DateTimeOffset? AddedAt(IElement link)
    {
        return long.TryParse(link.GetAttribute("add_date"), out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
    }
}
