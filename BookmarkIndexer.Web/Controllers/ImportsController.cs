using BookmarkIndexer.Core.Modules.Imports;
using BookmarkIndexer.Shared.Types.Imports;
using Microsoft.AspNetCore.Mvc;

namespace BookmarkIndexer.Web.Controllers;

[ApiController]
[Route("api/imports")]
public class ImportsController(ImportService imports) : ControllerBase
{
    [HttpPost]
    public async Task<ImportResultDto> Import(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        return await imports.Import(await reader.ReadToEndAsync());
    }

    [HttpPost("urls")]
    public async Task<ImportResultDto> ImportUrls(ImportUrlsDto dto)
    {
        return await imports.ImportUrls(dto.Urls);
    }
}
