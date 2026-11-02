# History

What changed and which existing features it affected. Newest first.

## 2026-11-02: Import Chrome and Edge exports
`FirefoxHtmlBookmarkImporter` is now `HtmlBookmarkImporter`: it reads every http(s) `<a>` link, which covers the
Netscape bookmark HTML that Firefox, Chrome and Edge all export. No parsing change.

Affected existing features:
- **Import:** the Import page explains how to export from Firefox, Chrome and Edge. A file with no http(s) bookmarks
  now fails with "Export bookmarks from Firefox, Chrome or Edge as HTML." instead of "Export bookmarks from Firefox as HTML.".

## 2026-11-02: Run in containers, README and license
`compose.yaml` runs Postgres, Ollama (with a one-off model pull), Lightpanda, the API and the UI; `Dockerfile` and
`ui/Dockerfile` build the API and UI images. Added `README.md` and an MIT `LICENSE`.

Affected existing features:
- **Scripts:** the API's log path in `appsettings.json` is now relative (`Logs/api/log-.txt`) instead of
  `<publish folder>/...`. `run.ps1` sets `Serilog__WriteTo__File__Args__path` so the published API still writes to
  `Logs\api` next to `Data`. `WriteTo` became a named object so that variable doesn't depend on sink order.

## 2026-11-02: Remove Seq
Affected existing features:
- **Scripts:** `run.ps1` no longer starts the Seq container and :8081 is gone. API logs go only to the console and the daily rolling file in `Logs\api` (14 days kept), which was already written before.

## 2026-11-01: Fix failed fetches
Plan: `docs/plans/fix-failed-fetches.md`, Stage 1.

Affected existing features:
- **Indexing:** Stack Exchange question links (stackoverflow, serverfault, superuser, askubuntu, *.stackexchange.com) are loaded from the Stack Exchange API, not Lightpanda. Their indexed text is the question and its answers, highest score first, without comments or page chrome. Over the API's 300 requests a day they fail with the API's throttle message. Pages that answer with an AWS WAF challenge (IMDb, HowStuffWorks) take a few seconds longer and are indexed instead of failing with "The page has no readable text.". When the reload redirects and the new URL is challenged again, the fetcher waits for that challenge too instead of failing with "HTTP 301".

## 2026-10-31: Index 4 bookmarks at a time
Plan: `docs/plans/bookmark-indexer.md`, Stage 4.

Affected existing features:
- **Indexing:** up to 4 bookmarks are indexed at once instead of one, still oldest first: about 30 a minute became Indexed, against about 7 before. A bookmark whose indexing is interrupted (the API stops) stays Pending instead of keeping a half-finished state.
- **Import:** re-importing a Pending bookmark that is being indexed at that moment waits until it is done (up to about 40 s) before its title and folder are updated.

## 2026-10-31: Review fixes
Plan: `docs/plans/bookmark-indexer.md`, section "Review fixes".

Affected existing features:
- **Search:** returns up to 50 meaning-based candidates instead of 40 before ranking, so more relevant bookmarks can appear. A snippet ends in "…" only when it was cut.
- **Indexing:** a page that doesn't finish loading within 30 s now fails with "The page did not load within 30 seconds." instead of hanging the worker. Re-indexing a bookmark replaces its old chunks instead of keeping them. Pages where Readability kept only a banner are indexed with their body text (already Indexed bookmarks keep their old text until re-indexed).
- **Bookmarks list:** a slow response from an older list request no longer overwrites a newer one; a Retry error stays visible after the next refresh.
- **Scripts:** `build.ps1` starts Postgres before the tests; `run.ps1` stops with an error when Postgres or Ollama isn't ready or the model pull fails, and stops the API and UI together.

Retro (no feature change): exit-code and plan-type gates in the hooks, an `ef_search` rule in the coding guideline, and new steps in the `add-integration`, `add-endpoint` and `rules-review` skills.

## 2026-10-30: Bookmark indexer, Stages 0–3
Plan: `docs/plans/bookmark-indexer.md`. New system: import, bookmarks list with Retry, index worker and hybrid search.

Affected existing features: none (first feature).
