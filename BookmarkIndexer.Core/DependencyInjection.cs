using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Core.Modules.Bookmarks;
using BookmarkIndexer.Core.Modules.Imports;
using BookmarkIndexer.Core.Modules.Indexing;
using BookmarkIndexer.Core.Modules.Search;
using BookmarkIndexer.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BookmarkIndexer.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Db")
            ?? throw new InvalidOperationException("ConnectionStrings:Db is not configured.");
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IBookmarkImporter, HtmlBookmarkImporter>();
        services.AddScoped<IBookmarkRepository, BookmarkRepository>();
        services.AddScoped<ImportService>();
        services.AddScoped<BookmarkService>();
        services.AddScoped<IndexService>();
        services.AddScoped<SearchService>();
        return services;
    }
}
