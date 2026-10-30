using BookmarkIndexer.Core.Model.Bookmarks;
using Microsoft.EntityFrameworkCore;

namespace BookmarkIndexer.Core.Model;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bookmark>(bookmark =>
        {
            // Import matches on URL: a URL is stored once, whatever folder it appears in.
            bookmark.HasIndex(b => b.Url).IsUnique();
            bookmark.HasIndex(b => b.Status);
            bookmark.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}
