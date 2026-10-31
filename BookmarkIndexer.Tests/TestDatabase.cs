using BookmarkIndexer.Core.Model;
using Microsoft.EntityFrameworkCore;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace BookmarkIndexer.Tests;

// Search depends on pgvector and tsvector, so tests use the local Postgres container (started by build.ps1 and run.ps1)
// instead of SQLite. Tests run one at a time because each one starts from empty tables.
public static class TestDatabase
{
    const string ConnectionString = "Host=localhost;Port=5432;Database=bookmarkindexer_test;Username=postgres;Password=postgres";
    static bool isMigrated;

    public static async Task<AppDbContext> CreateEmpty()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.UseVector()).Options);
        if (!isMigrated)
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
            isMigrated = true;
        }
        await db.Database.ExecuteSqlRawAsync("""TRUNCATE "Bookmarks" RESTART IDENTITY CASCADE""");
        return db;
    }
}
