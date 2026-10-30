using BookmarkIndexer.Core.Modules.Search;
using BookmarkIndexer.Shared.Types.Search;
using Microsoft.AspNetCore.Mvc;

namespace BookmarkIndexer.Web.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController(SearchService search) : ControllerBase
{
    [HttpGet]
    public async Task<List<SearchResultDto>> Search(string? q)
    {
        return await search.Search(q);
    }
}
