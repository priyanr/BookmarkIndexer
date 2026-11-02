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
            // AWS WAF (IMDb, HowStuffWorks) first answers 202 with a JavaScript challenge. Lightpanda solves it and the
            // page reloads with the real content a few seconds later. The reload can redirect (an old URL moved) and the
            // new URL can be challenged again, so the wait skips 3xx and repeats until a response is not a challenge.
            while (response != null && await response.HeaderValueAsync("x-amzn-waf-action") == "challenge")
            {
                response = await page.WaitForResponseAsync(r => r.Request.IsNavigationRequest && r.Status is < 300 or >= 400);
                await page.WaitForLoadStateAsync(LoadState.Load);
            }
            if (response is { Ok: false })
            {
                throw new HttpRequestException($"HTTP {response.Status} {response.StatusText}".Trim());
            }
            return await page.ContentAsync();
        }
    }
}
