using BookmarkIndexer.Shared;
using Microsoft.Playwright;

namespace BookmarkIndexer.Integrations.Lightpanda;

/// <summary>Loads pages in the Lightpanda container over CDP. A Playwright-driven Chromium fetcher would differ only in how it gets the browser.</summary>
public class LightpandaPageFetcher(string cdpUrl) : IPageFetcher
{
    const float PageTimeoutMs = 30_000;

    public async Task<string> FetchHtml(string url, CancellationToken ct)
    {
        // Lightpanda serves one browser context per connection, so every page gets its own connection.
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.ConnectOverCDPAsync(cdpUrl);
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync(url, new PageGotoOptions { Timeout = PageTimeoutMs }).WaitAsync(ct);
        if (response is { Ok: false })
        {
            throw new HttpRequestException($"HTTP {response.Status} {response.StatusText}".Trim());
        }
        return await page.ContentAsync();
    }
}
