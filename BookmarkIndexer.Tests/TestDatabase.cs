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
        if (!isMigrated)
        {
            // DROP and CREATE DATABASE force a checkpoint, which takes over a minute while the API is indexing into the
            // same container. A drop cut off by the 30 s default leaves the database invalid and every later test fails.
            await using var setupDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString, npgsql => npgsql.UseVector().CommandTimeout(300)).Options);
            await setupDb.Database.EnsureDeletedAsync();
            await setupDb.Database.MigrateAsync();
            isMigrated = true;
        }
        var db = Connect();
        await db.Database.ExecuteSqlRawAsync("""TRUNCATE "Bookmarks" RESTART IDENTITY CASCADE""");
        return db;
    }

    /// <summary>Another connection to the same data, for tests where two workers run at once. Call CreateEmpty first.</summary>
    public static AppDbContext Connect()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.UseVector()).Options);
    }
}
