using BookmarkIndexer.Core.Modules.Imports;
using BookmarkIndexer.Shared.Exceptions;
using BookmarkIndexer.Shared.Types.Bookmarks;
using BookmarkIndexer.Shared.Types.Imports;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookmarkIndexer.Tests.Imports;

public class ImportServiceTests
{
    const string FirstExport = """
        <DL><p>
            <DT><A HREF="https://a.example/">A</A>
            <DT><A HREF="https://b.example/">B</A>
        </DL>
        """;

    // B is renamed and moved into a folder, A is unchanged, C is new.
    const string SecondExport = """
        <DL><p>
            <DT><A HREF="https://a.example/">A</A>
            <DT><H3>Reading</H3>
            <DL><p>
                <DT><A HREF="https://b.example/">B renamed</A>
            </DL><p>
            <DT><A HREF="https://c.example/">C</A>
        </DL>
        """;

    [Fact]
    public async Task First_import_adds_every_bookmark_as_pending()
    {
        await using var db = await TestDatabase.CreateEmpty();

        var result = await new ImportService(db, new HtmlBookmarkImporter()).Import(FirstExport);

        Assert.Equal(new ImportResultDto(Added: 2, Updated: 0, Skipped: 0), result);
        Assert.All(await db.Bookmarks.ToListAsync(), b => Assert.Equal(IndexStatus.Pending, b.Status));
    }

    [Fact]
    public async Task Re_import_adds_only_new_urls_and_keeps_the_index_status_of_existing_ones()
    {
        await using var db = await TestDatabase.CreateEmpty();
        var service = new ImportService(db, new HtmlBookmarkImporter());
        await service.Import(FirstExport);
        await db.Bookmarks.ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, IndexStatus.Indexed));

        var result = await service.Import(SecondExport);

        Assert.Equal(new ImportResultDto(Added: 1, Updated: 1, Skipped: 1), result);
        var bookmarks = await db.Bookmarks.AsNoTracking().OrderBy(b => b.Url).ToListAsync();
        Assert.Equal(
            [
                ("https://a.example/", "A", "", IndexStatus.Indexed),
                ("https://b.example/", "B renamed", "Reading", IndexStatus.Indexed),
                ("https://c.example/", "C", "", IndexStatus.Pending),
            ],
            bookmarks.Select(b => (b.Url, b.Title, b.FolderPath, b.Status)));
    }

    [Fact]
    public async Task A_file_without_bookmarks_is_rejected()
    {
        await using var db = await TestDatabase.CreateEmpty();

        var error = await Assert.ThrowsAsync<BadRequestException>(() => new ImportService(db, new HtmlBookmarkImporter()).Import("<html></html>"));

        Assert.Equal("The file contains no http(s) bookmarks. Export bookmarks from Firefox, Chrome or Edge as HTML.", error.Message);
    }

    [Fact]
    public async Task Pasted_urls_are_added_as_pending_ignoring_blank_and_non_http_lines_and_duplicates()
    {
        await using var db = await TestDatabase.CreateEmpty();

        var result = await new ImportService(db, new HtmlBookmarkImporter()).ImportUrls("""
            https://a.example/

            javascript:alert(1)
            https://b.example/
            https://a.example/
            """);

        Assert.Equal(new ImportResultDto(Added: 2, Updated: 0, Skipped: 0), result);
        var bookmarks = await db.Bookmarks.AsNoTracking().OrderBy(b => b.Url).ToListAsync();
        Assert.Equal(
            [
                ("https://a.example/", "", "", IndexStatus.Pending),
                ("https://b.example/", "", "", IndexStatus.Pending),
            ],
            bookmarks.Select(b => (b.Url, b.Title, b.FolderPath, b.Status)));
    }

    [Fact]
    public async Task Re_pasting_a_known_url_leaves_its_title_and_status_untouched()
    {
        await using var db = await TestDatabase.CreateEmpty();
        var service = new ImportService(db, new HtmlBookmarkImporter());
        await service.Import(FirstExport);
        await db.Bookmarks.ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, IndexStatus.Indexed));

        var result = await service.ImportUrls("https://a.example/\nhttps://c.example/");

        Assert.Equal(new ImportResultDto(Added: 1, Updated: 0, Skipped: 1), result);
        var a = await db.Bookmarks.AsNoTracking().SingleAsync(b => b.Url == "https://a.example/");
        Assert.Equal(("A", IndexStatus.Indexed), (a.Title, a.Status));
    }

    [Fact]
    public async Task Pasted_text_without_http_urls_is_rejected()
    {
        await using var db = await TestDatabase.CreateEmpty();

        var error = await Assert.ThrowsAsync<BadRequestException>(() => new ImportService(db, new HtmlBookmarkImporter()).ImportUrls("not a url\n"));

        Assert.Equal("Paste at least one http(s) URL, one per line.", error.Message);
    }
}
