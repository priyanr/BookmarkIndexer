using BookmarkIndexer.Core.Modules.Bookmarks;
using BookmarkIndexer.Shared.Types;
using BookmarkIndexer.Shared.Types.Bookmarks;
using Microsoft.AspNetCore.Mvc;

namespace BookmarkIndexer.Web.Controllers;

[ApiController]
[Route("api/bookmarks")]
public class BookmarksController(BookmarkService bookmarks) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResultDto<BookmarkDto>> List(IndexStatus? status, int page = 1)
    {
        return await bookmarks.List(status, page);
    }

    [HttpGet("counts")]
    public async Task<BookmarkCountsDto> Counts()
    {
        return await bookmarks.Counts();
    }

    [HttpPost("{id}/retry")]
    public async Task<IActionResult> Retry(int id)
    {
        await bookmarks.Retry(id);
        return NoContent();
    }

    [HttpPost("retry-failed")]
    public async Task<IActionResult> RetryFailed()
    {
        await bookmarks.RetryFailed();
        return NoContent();
    }
}
