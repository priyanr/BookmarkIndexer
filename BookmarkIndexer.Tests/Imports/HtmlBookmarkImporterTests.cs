using BookmarkIndexer.Core.Modules.Imports;
using Xunit;

namespace BookmarkIndexer.Tests.Imports;

public class HtmlBookmarkImporterTests
{
    const string Export = """
        <!DOCTYPE NETSCAPE-Bookmark-file-1>
        <META HTTP-EQUIV="Content-Type" CONTENT="text/html; charset=UTF-8">
        <TITLE>Bookmarks</TITLE>
        <H1>Bookmarks Menu</H1>
        <DL><p>
            <DT><A HREF="https://example.com/top" ADD_DATE="1700000000" LAST_MODIFIED="1700000001">Top level</A>
            <DT><H3 ADD_DATE="1600000000">Dev</H3>
            <DL><p>
                <DT><A HREF="https://learn.microsoft.com/dotnet" ADD_DATE="1600000100">.NET docs</A>
                <DT><H3>Postgres</H3>
                <DL><p>
                    <DT><A HREF="http://postgresql.org/docs">PostgreSQL &amp; docs</A>
                </DL><p>
                <DT><A HREF="place:sort=8&amp;maxResults=10">Recently tagged</A>
                <DT><A HREF="javascript:void(0)">Bookmarklet</A>
            </DL><p>
            <DT><H3 PERSONAL_TOOLBAR_FOLDER="true">Bookmarks Toolbar</H3>
            <DL><p>
                <DT><A HREF="https://example.com/top">Duplicate of top level</A>
            </DL><p>
        </DL>
        """;

    [Fact]
    public void Reads_http_bookmarks_with_their_folder_path()
    {
        var bookmarks = new HtmlBookmarkImporter().Read(Export);

        Assert.Equal(
            [
                new("https://example.com/top", "Top level", "", DateTimeOffset.FromUnixTimeSeconds(1700000000)),
                new("https://learn.microsoft.com/dotnet", ".NET docs", "Dev", DateTimeOffset.FromUnixTimeSeconds(1600000100)),
                new("http://postgresql.org/docs", "PostgreSQL & docs", "Dev/Postgres", null),
            ],
            bookmarks);
    }

    [Fact]
    public void Content_without_bookmarks_gives_an_empty_list()
    {
        Assert.Empty(new HtmlBookmarkImporter().Read("<html><body>not an export</body></html>"));
    }
}
