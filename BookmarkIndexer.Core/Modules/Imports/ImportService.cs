using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Core.Model.Bookmarks;
using BookmarkIndexer.Shared;
using BookmarkIndexer.Shared.Exceptions;
using BookmarkIndexer.Shared.Types.Bookmarks;
using BookmarkIndexer.Shared.Types.Imports;
using Microsoft.EntityFrameworkCore;

namespace BookmarkIndexer.Core.Modules.Imports;

public class ImportService(AppDbContext db, IBookmarkImporter importer)
{
    public async Task<ImportResultDto> Import(string content)
    {
        var imported = importer.Read(content);
        if (imported.Count == 0)
        {
            throw new BadRequestException("The file contains no http(s) bookmarks. Export bookmarks from Firefox, Chrome or Edge as HTML.");
        }

        var urls = imported.Select(b => b.Url).ToList();
        var existing = await db.Bookmarks.Where(b => urls.Contains(b.Url)).ToDictionaryAsync(b => b.Url);

        int added = 0, updated = 0, skipped = 0;
        foreach (var item in imported)
        {
            if (!existing.TryGetValue(item.Url, out var bookmark))
            {
                db.Bookmarks.Add(new Bookmark { Url = item.Url, Title = item.Title, FolderPath = item.FolderPath, AddedAt = item.AddedAt, Status = IndexStatus.Pending });
                added++;
                continue;
            }
            // An existing URL keeps its content and index status; only its title and folder follow the newer export.
            if (bookmark.Title == item.Title && bookmark.FolderPath == item.FolderPath)
            {
                skipped++;
                continue;
            }
            bookmark.Title = item.Title;
            bookmark.FolderPath = item.FolderPath;
            updated++;
        }

        await db.SaveChangesAsync();
        return new ImportResultDto(added, updated, skipped);
    }

    public async Task<ImportResultDto> ImportUrls(string text)
    {
        var urls = ParseUrls(text);
        if (urls.Count == 0)
        {
            throw new BadRequestException("Paste at least one http(s) URL, one per line.");
        }

        var existing = await db.Bookmarks.Where(b => urls.Contains(b.Url)).Select(b => b.Url).ToListAsync();
        var existingUrls = existing.ToHashSet();

        int added = 0;
        foreach (var url in urls)
        {
            if (existingUrls.Contains(url))
            {
                continue;
            }
            db.Bookmarks.Add(new Bookmark { Url = url, Title = "", FolderPath = "", Status = IndexStatus.Pending });
            added++;
        }

        await db.SaveChangesAsync();
        return new ImportResultDto(added, 0, urls.Count - added);

        static List<string> ParseUrls(string text)
        {
            var urls = new List<string>();
            var seenUrls = new HashSet<string>();
            foreach (var line in text.Split('\n'))
            {
                var url = line.Trim();
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (seenUrls.Add(url))
                {
                    urls.Add(url);
                }
            }
            return urls;
        }
    }
}
