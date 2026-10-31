# Status

What works today, what is known to be wrong, and what is not built. Update it when a change lands.

## Works
- **Import:** upload a Firefox bookmarks HTML export. New URLs become Pending; existing ones get their title and folder updated and keep their content.
- **Bookmarks list:** tabs for All, Pending, Indexed and Failed with counts, paged 50 at a time, refreshed every 5 s. Retry one Failed bookmark or all of them.
- **Indexing:** a background worker takes the oldest Pending bookmark and loads it in Lightpanda (30 s timeout for the whole fetch). It extracts the readable text (Readability, falling back to the body text when Readability keeps only a banner), splits it into chunks, embeds them with Ollama and marks the bookmark Indexed. On error the bookmark becomes Failed with the message.
- **Search:** hybrid vector and full-text search over chunks, merged with reciprocal rank fusion, top 20 bookmarks with a snippet.
- **Scripts:** `build.ps1` builds, tests and publishes; `run.ps1` starts the containers, the API (:5080) and the UI (:3000). Logs go to Seq (:8081) and `Logs\api`.

## Known issues
See `docs/issues_accepted.md`:
- Extracted text glues neighbouring words together, which weakens full-text matches.

## Not built
Authentication, other export formats, a Playwright fetcher, other embedding APIs, parallel fetching, scheduled re-indexing, deleting bookmarks and import history (plan: Out of scope).
