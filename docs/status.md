# Status

What works today, what is known to be wrong, and what is not built. Update it when a change lands.

## Works
- **Import:** upload a bookmarks HTML export from Firefox, Chrome or Edge. New URLs become Pending; existing ones get their title and folder updated and keep their content. Or paste a list of URLs (one per line); new ones become Pending with no title or folder, URLs already imported are left as they are.
- **Bookmarks list:** tabs for All, Pending, Indexed and Failed with counts, paged 50 at a time, refreshed every 5 s. Retry one Failed bookmark or all of them. Open an Indexed bookmark's page content (the extracted text) with its character count.
- **Indexing:** a background worker runs 4 loops; each claims the oldest Pending bookmark no other loop holds and loads it in Lightpanda (30 s timeout for the whole fetch). Stack Exchange question links are loaded from the Stack Exchange API instead, because Cloudflare blocks Lightpanda there (300 requests a day without an app key). Pages behind an AWS WAF challenge (IMDb, HowStuffWorks) are read after the challenge reloads them. It extracts the readable text (Readability, falling back to the body text when Readability keeps only a banner), splits it into chunks, embeds them with Ollama and marks the bookmark Indexed. On error the bookmark becomes Failed with the message.
- **Search:** hybrid vector and full-text search over chunks, merged with reciprocal rank fusion, top 20 bookmarks with a snippet. Each result can open its page content.
- **Scripts:** `build.ps1` builds, tests and publishes; `run.ps1` starts the containers, the API (:5080) and the UI (:3000). Logs go to a daily rolling file in `Logs\api` (14 days kept). `docker compose up -d --build` runs the whole stack in containers instead (see `README.md`).

## Known issues
See `docs/issues_accepted.md`:
- Extracted text glues neighbouring words together, which weakens full-text matches.

## Not built
Authentication, other export formats, a Playwright fetcher, other embedding APIs, scheduled re-indexing, deleting bookmarks and import history (plan: Out of scope).
