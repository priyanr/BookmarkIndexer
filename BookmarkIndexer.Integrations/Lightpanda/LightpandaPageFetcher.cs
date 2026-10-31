using BookmarkIndexer.Shared;
using Microsoft.Playwright;

namespace BookmarkIndexer.Integrations.Lightpanda;

/// <summary>Loads pages in the Lightpanda container over CDP. A Playwright-driven Chromium fetcher would differ only in how it gets the browser.</summary>
public class LightpandaPageFetcher(string cdpUrl) : IPageFetcher
{
    public async Task<string> FetchHtml(string url, CancellationToken ct)
    {
        // Playwright calls take no token, so the whole fetch is abandoned when the caller gives up (timeout or shutdown).
        return await Fetch().WaitAsync(ct);

        async Task<string> Fetch()
        {
            // Lightpanda serves one browser context per connection, so every page gets its own connection.
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.ConnectOverCDPAsync(cdpUrl);
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            var response = await page.GotoAsync(url);
            if (response is { Ok: false })
            {
                throw new HttpRequestException($"HTTP {response.Status} {response.StatusText}".Trim());
            }
            return await page.ContentAsync();
        }
    }
}
