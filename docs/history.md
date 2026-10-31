# History

What changed and which existing features it affected. Newest first.

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
