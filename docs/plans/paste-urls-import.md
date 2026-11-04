# Paste URLs to import

## Goal
On the Import page, paste a list of URLs (one per line) into a text box and import them as new Pending bookmarks,
without needing a bookmarks export file.

## Affected existing features
None. The HTML file import (API, parsing, merge behaviour) is unchanged; the Import page gains a second form.

## Stages
### Stage 1: Paste-URLs import
- Behaviour: `POST api/imports/urls` with `{ "urls": "<text>" }` parses one http(s) URL per line (blank and
  non-http(s) lines ignored, duplicates collapsed). A new URL becomes a Pending bookmark with no title or folder;
  a URL already in the database is left untouched and counted as skipped (pasted URLs carry no title/folder to
  update, unlike the HTML export). Empty/invalid input throws `BadRequestException("Paste at least one http(s)
  URL, one per line.")`. The Import page gets a second form: a textarea and an "Import URLs" button, sharing the
  existing added/updated/skipped result message with the file-upload form.
- Test seam: `ImportService` with a real database (`ImportServiceTests`).
- New types: `ImportUrlsDto` (Shared/Types/Imports) — the request body, so the UI's JSON client can send the
  pasted text.
- Files touched: `ImportUrlsDto.cs` (new), `ImportService.cs`, `ImportsController.cs`, `ImportServiceTests.cs`,
  `bookmarksApi.js`, `bookmarksSlice.js`, `import/page.js`, `GLOSSARY.md`, docs.
- [x] done

## Out of scope
- Detecting titles for pasted URLs (would require fetching each page before import).
- Separators other than newlines (commas, spaces).
- A combined single form for both import types.
