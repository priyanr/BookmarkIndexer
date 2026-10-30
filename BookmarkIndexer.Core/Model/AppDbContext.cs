using Microsoft.EntityFrameworkCore;

namespace BookmarkIndexer.Core.Model;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
