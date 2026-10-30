using BookmarkIndexer.Core.Model.Bookmarks;
using Microsoft.EntityFrameworkCore;

namespace BookmarkIndexer.Core.Model;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // nomic-embed-text returns 768 dimensions; another embedding model needs a migration that changes this.
    public const int EmbeddingDimensions = 768;

    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<BookmarkChunk> Chunks => Set<BookmarkChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Bookmark>(bookmark =>
        {
            // Import matches on URL: a URL is stored once, whatever folder it appears in.
            bookmark.HasIndex(b => b.Url).IsUnique();
            bookmark.HasIndex(b => b.Status);
            bookmark.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
            bookmark.HasMany(b => b.Chunks).WithOne().HasForeignKey(c => c.BookmarkId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BookmarkChunk>(chunk =>
        {
            chunk.Property(c => c.Embedding).HasColumnType($"vector({EmbeddingDimensions})");
            chunk.HasIndex(c => c.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
            chunk.HasGeneratedTsVectorColumn(c => c.SearchVector, "english", c => new { c.Text })
                .HasIndex(c => c.SearchVector).HasMethod("GIN");
            chunk.HasIndex(c => new { c.BookmarkId, c.Position }).IsUnique();
        });
    }
}
