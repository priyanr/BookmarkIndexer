# Fix failed fetches

## Goal
Bookmarks that failed only because a site challenged the headless browser get indexed: Stack Exchange questions
through the Stack Exchange API, and AWS WAF protected sites (IMDb, HowStuffWorks) by waiting for the challenge.

## Findings (2026-10-31, 2357 Failed bookmarks)
| Group | Count | Cause | Fixable |
|---|---|---|---|
| HTTP 404 / 410, CouldntResolveHost | ~1100 | Page or domain gone | No |
| Stack Exchange sites (stackoverflow, serverfault, superuser, askubuntu, *.stackexchange.com) | 304 | Cloudflare challenge answers 403, also after waiting | **Yes**, via the API |
| imdb.com, *.howstuffworks.com | ~105 | AWS WAF answers 202 with an empty challenge page; Lightpanda solves it and the page reloads 3–4 s later, but we read the page before that | **Yes**, wait for the reload |
| Other 403 (osdev, gamedev, nytimes, medium, …) | ~280 | Cloudflare and similar challenges Lightpanda can't pass | No |
| HTTP 429 (hashicorp, …) | 17 | Vercel bot challenge, not a rate limit; retrying gives 429 again | No |
| codeproject.com "no readable text" | 45 | Domain now redirects to a parked `/lander` page | No |
| Social networks, private hosts | ~200 | Login walls, hosts not reachable from outside | No |

## Affected existing features
- **Indexing:** Stack Exchange question URLs are fetched from `api.stackexchange.com` instead of being loaded in
  Lightpanda; the indexed text is the question and its answers (no comments, no page chrome). Pages that answer with an
  AWS WAF challenge take a few seconds longer and are indexed instead of failing with "The page has no readable text.".
- No API, UI or data shape changes. The affected Failed bookmarks are set back to Pending once, by hand.

## Stages
### Stage 1: AWS WAF challenge and Stack Exchange API
- Behaviour: a Stack Exchange question URL is indexed with the question and answer text; an IMDb title page is indexed.
- Test seam: `StackExchangePageFetcher` with a fake `HttpMessageHandler` (request URL, returned HTML, API error,
  missing question, other sites passed on). The Lightpanda wait needs a real browser: checked by hand against the
  running container.
- New types: `StackExchangePageFetcher` (Integrations/StackExchange): the `IPageFetcher` that loads Stack Exchange
  questions from the API and passes every other URL to Lightpanda, because Cloudflare blocks Lightpanda on those sites.
- Files touched: `LightpandaPageFetcher.cs`, `Program.cs`, docs.
- [x] done

## Rollout
The running API is the published one, so publish first (`build.ps1`, with `run.ps1` stopped), start it again, then set
the 418 affected Failed bookmarks (304 Stack Exchange, 114 IMDb/HowStuffWorks) back to Pending. Doing this before
publishing would make the old code fail them again.

```sql
UPDATE "Bookmarks" SET "Status" = 'Pending', "Error" = NULL
WHERE "Status" = 'Failed'
  AND ("Url" ~* '^https?://(www\.)?([a-z0-9-]+\.)?(stackoverflow\.com|stackexchange\.com|serverfault\.com|superuser\.com|askubuntu\.com|mathoverflow\.net|stackapps\.com)/questions/[0-9]+'
       OR "Url" ~* '(imdb\.com|howstuffworks\.com)');
```
Run it with `wslc exec -i bookmarkindexer-postgres psql -U postgres -d bookmarkindexer`. A few Stack Exchange
bookmarks may hit the 300-a-day quota; retry those the next day.

## After rollout (2026-11-01)
379 of the 418 were indexed. 23 HowStuffWorks pages failed with "HTTP 301": after the challenge the reload redirects
(an old URL moved) and the new URL is challenged again. The wait took the redirect as the final response. It now skips
3xx and repeats while the response is a challenge. Checked by hand on three of the 23 URLs and an IMDb title page; an
automated test would need a fake WAF server reachable from the Lightpanda container. Set those 23 back to Pending after
publishing:

```sql
UPDATE "Bookmarks" SET "Status" = 'Pending', "Error" = NULL
WHERE "Status" = 'Failed' AND "Url" ~* 'howstuffworks\.com' AND "Error" = 'HTTP 301';
```

## Out of scope
- A Stack Exchange app key (raises the quota from 300 to 10,000 requests a day). 304 bookmarks fit in about one day
  without it; the rest fail with the API's throttle message and can be retried the next day.
- Answer links (`/a/<id>`) and comments; all failed Stack Exchange bookmarks are `/questions/<id>` links.
- Other challenge types (Cloudflare, Vercel) and the other groups above.
