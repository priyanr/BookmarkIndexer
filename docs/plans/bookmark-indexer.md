# Bookmark Indexer

## Context
The repo is an empty scaffold: rules, hooks, `tests/ArchitectureTests.cs` and the props files, with `AppName` placeholders. The goal is to index every Firefox bookmark. Import the exported HTML, load each URL in headless Lightpanda, extract the readable text, embed it with Ollama and store it in Postgres with pgvector. A Next.js UI lets the user import, browse bookmarks by status and search them. The build and run scripts go in `<publish folder>` (the empty `build.ps1` and `run.ps1` there). Containers run with `wslc`, which uses Docker-like syntax.

First step of execution: copy this plan to `docs/plans/bookmark-indexer.md` (workflow rule).

## Decisions (from Q&A)
- Embeddings come from Ollama (`nomic-embed-text`, 768 dimensions) running in wslc, behind a swappable abstraction.
- The UI is Next.js using JavaScript, App Router and Redux Toolkit (`createSlice` + `createAsyncThunk`, no RTK Query). All HTTP calls live in `ui/src/api/bookmarksApi.js`, and thunks call them. The UI runs as its own Node server (`output: 'standalone'`). Next `rewrites` proxy `/api/*` to the API, so the API needs no CORS setup.
- Search is hybrid: pgvector cosine similarity plus Postgres full-text, merged with reciprocal rank fusion (RRF).
- Lightpanda runs in a wslc container with CDP on port 9222. The API connects with `Microsoft.Playwright` `ConnectOverCDPAsync`, so a later Playwright fetcher uses the same library.
- Logs go through Serilog to a Seq container (data in `Logs\seq`) and to rolling files in `Logs\api`.
- Processing is a background worker that handles one bookmark at a time with a timeout. Failures keep their error message, and the UI has a Retry action.
- On re-import, a URL that already exists is skipped as content, but its title and folder are updated. Only new URLs become Pending.
- Text extraction uses the `SmartReader` package (a Readability port). AngleSharp comes with it and also parses the Firefox HTML.

## Conflicts with the scaffold
- **Rename:** `AppName` becomes `BookmarkIndexer` in CLAUDE.md, the tests, the namespaces and the migration command.
- **Web project:** The guideline describes `BookmarkIndexer.Web` as MVC with Razor. Here it becomes an API-only project with controllers, the worker and `Program.cs`. The UI lives in `ui/` (Next.js), outside the .NET solution. I'll update the Layout line in CLAUDE.md.
- **"Provider" naming:** `ArchitectureTests` bans type names ending in `Provider`. The swap points you asked for keep their role but take these names:
  - `IBookmarkImporter` (Shared), implemented by `FirefoxHtmlBookmarkImporter` (Core/Modules/Imports). Later formats get another implementation.
  - `IPageFetcher` (Shared), implemented by `LightpandaPageFetcher` (Integrations/Lightpanda). A later `PlaywrightPageFetcher` is just another registration.
  - Embeddings use `Microsoft.Extensions.AI`'s `IEmbeddingGenerator<string, Embedding<float>>`, implemented by `OllamaApiClient` from the `OllamaSharp` package. This needs no interface of our own. Moving to OpenAI or another API later is a one-line DI change.
- **Tests:** The guideline says SQLite in-memory, but pgvector and `tsvector` need real Postgres. Service and repository tests therefore run against a `bookmarkindexer_test` database on the local wslc Postgres, which is migrated and wiped per test class. Docker-based Testcontainers are not used because they don't drive wslc.
- **New packages (all need your OK, central in Directory.Packages.props):**
  - API: AngleSharp (direct reference in Core for the Firefox importer; SmartReader depends on it too), Npgsql.EntityFrameworkCore.PostgreSQL, Pgvector.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design, Microsoft.Playwright, SmartReader, OllamaSharp, Microsoft.Extensions.AI.Abstractions, Serilog.AspNetCore, Serilog.Sinks.Seq, Serilog.Sinks.File.
  - Tests: xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, Microsoft.Extensions.TimeProvider.Testing.
  - UI npm: next, react, react-dom, @reduxjs/toolkit, react-redux.

## Domain (GLOSSARY.md, replacing the example rows)
| Term | Meaning | In code |
|---|---|---|
| Bookmark | One saved URL from a browser export, unique by URL | `Bookmark` entity |
| Import | Loading a bookmark export file into the system | `ImportService`, `IBookmarkImporter` |
| Index status | Where a bookmark is in processing: Pending, Indexed or Failed | `IndexStatus` enum (Shared) |
| Chunk | A slice of a bookmark's extracted text with its embedding | `BookmarkChunk` entity |

## Data
- `Bookmark` has these fields: Id, Url (unique index), Title, FolderPath, AddedAt (from Firefox `ADD_DATE`), Status, Error, Attempts, IndexedAt and Chunks.
- `BookmarkChunk` has these fields: Id, BookmarkId, Position, Text, `Embedding vector(768)` (HNSW cosine index) and `SearchVector tsvector` (a column generated from Text, with a GIN index). It is a child of Bookmark, so it gets no repository of its own.
- Containers on the wslc network, ports as follows:
  - `pgvector/pgvector:pg17` on 5432, with data in `Data\postgres`.
  - `datalust/seq` on 5341 and UI port 8081, with data in `Logs\seq`.
  - `ollama/ollama` on 11434, with data in `Data\ollama`.
  - `lightpanda/browser:nightly` on 9222.
- The API runs on 5080 and the UI on 3000.

## Stages

### Stage 0: Scaffold, containers and scripts
- **Behaviour:**
  - `build.ps1` builds with `-warnaserror`, runs the tests, publishes the API to `...\Api` and builds the Next standalone app into `...\Ui` (including `.next/static` and `public`).
  - `run.ps1` creates or starts the four wslc containers with their mounts, pulls `nomic-embed-text` if it is missing, then starts the API (`dotnet BookmarkIndexer.Web.dll`) and the UI (`node server.js`).
  - `GET /api/health` responds, Serilog logs reach Seq and `Logs\api`, and the UI shell renders.
- **Test seam:** ArchitectureTests pass once projects exist.
- **New types:** `AppDbContext` (EF context).
- **Files touched:** `BookmarkIndexer.sln`, the 5 csproj files, `Directory.Packages.props`, `CLAUDE.md`, `GLOSSARY.md`, `tests/ArchitectureTests.cs` (moved to `BookmarkIndexer.Tests/`, renamed), `ui/` skeleton (`package.json`, `next.config.js`, `src/app/layout.js`, `src/store/store.js`), `<publish folder>\build.ps1` and `run.ps1`.
- [x] done

### Stage 1: Import, then list as Pending
- **Behaviour:** On the Import page you upload a Firefox `.html` file. `POST /api/imports` (multipart) parses it and saves new URLs as Pending, updating the title and folder of existing ones. It returns `ImportResultDto(added, updated, skipped)`. The Bookmarks page shows tabs for All, Pending, Indexed and Failed with counts, plus a paged list (`GET /api/bookmarks?status=&page=`).
- **Test seam:**
  - `FirefoxHtmlBookmarkImporter` is a pure test on a sample export with nested folders and a non-http `place:` URL that must be skipped.
  - `ImportService` uses the real test DB and checks that re-import only adds new URLs.
- **New types:**
  - Entities: `Bookmark`. Enum: `IndexStatus`. Interface: `IBookmarkImporter` (the swap point for export formats). Class: `FirefoxHtmlBookmarkImporter`.
  - Services: `ImportService`, `BookmarkService` (list and counts). No repository yet: both queries have one caller, so they stay inline (coding guideline). `IBookmarkRepository` arrives with the first aggregate load in Stage 2.
  - `BadRequestException` (Shared): input errors become HTTP 400 with the message, mapped once in `Program.cs`.
  - Controllers: `ImportsController`, `BookmarksController`.
  - DTOs: `ImportedBookmarkDto` (importer output), `ImportResultDto`, `BookmarkDto`, `BookmarkCountsDto`, `PagedResultDto<T>`.
  - UI: `bookmarksApi.js`, `bookmarksSlice.js` and the import and bookmarks pages.
- [x] done

### Stage 2a: Index worker (fetch and extract)
- **Behaviour:**
  - `IndexWorker` (a BackgroundService that honours its cancellation token) loops through `IndexService.IndexNext()`. That method takes the oldest Pending bookmark and fetches its HTML through `IPageFetcher` (Lightpanda over CDP), with a 30 s timeout.
  - The readable text is extracted with SmartReader and stored on the bookmark (`Content`), which is then marked Indexed. Stage 2b chunks it from there, so changing the chunking never needs a refetch.
  - On error, or when the page has no readable text, the bookmark is marked Failed with the message. Attempts counts every try.
  - When nothing is Pending, the worker waits 10 s before checking again.
  - The Bookmarks page refreshes every 5 s, so progress is visible.
- **Test seam:** `IndexService` uses a fake `IPageFetcher` and `FakeTimeProvider` against the real test DB, covering success, fetch failure, an unreadable page and nothing Pending. `LightpandaPageFetcher` gets a manual smoke test.
- **New types:** `IPageFetcher` (the browser swap point), `LightpandaPageFetcher`, `IndexService`, `IndexWorker`, and `IBookmarkRepository`/`BookmarkRepository` (`GetNextPendingOrNull` is the first aggregate load).
- **Changed at the user's request:** the List and Counts queries moved from `BookmarkService` into `BookmarkRepository` (`ListByStatus`, `CountByStatus`), with `BookmarkRepositoryTests`.
- [x] done

### Stage 2b: Chunks, embeddings and Retry
- **Behaviour:**
  - Indexing splits `Content` into chunks of about 1,000 characters with 150 characters of overlap, embeds them with Ollama and saves them as `BookmarkChunk`s (pgvector).
  - `POST /api/bookmarks/{id}/retry` and `POST /api/bookmarks/retry-failed` reset bookmarks to Pending. The UI has Retry buttons.
- **Test seam:** chunking is a pure static rule test. `IndexService` gets a fake `IEmbeddingGenerator`. The retry service runs against the real test DB.
- **New types:** `BookmarkChunk` entity, `TextChunks` (a static chunking rule with tests) and `NotFoundException` (Shared; retrying an unknown bookmark returns 404, mapped in `Program.cs`).
- [x] done

### Stage 3: Hybrid search
- **Behaviour:** `GET /api/search?q=` embeds the query. One SQL query takes the top 50 chunks by vector similarity and the top 50 by `ts_rank` (`websearch_to_tsquery`), merges them with RRF (k = 60) and groups them by bookmark. It returns the top 20 as `SearchResultDto(bookmarkId, url, title, folderPath, snippet, score)`. The Search page has a box and a results list that links out to each page.
- **Test seam:** `BookmarkRepository.Search` runs against the real test DB with seeded chunks and hand-made embeddings. An exact-keyword match must rank, and so must a near-vector match.
- **New types:** `SearchResultDto`, `SearchController`, and the search page and slice in the UI.
- [x] done

## Review fixes (after Stage 3)
From the rules review of the whole feature:
- Search sets `hnsw.ef_search = 100` (in a transaction) so the HNSW index really yields 50 vector candidates, not its default 40.
- The 30 s timeout covers the whole page fetch (connect, load, read), driven by `TimeProvider` in `IndexService`; `LightpandaPageFetcher` honours the token for the whole call.
- `GetNextPendingOrNull` includes `Chunks`, and indexing replaces any existing chunks.
- Text extraction moved into `PageText` (static rule): when Readability finds little text (< 500 characters) but the page body holds more than three times as much, the body text is indexed instead (e.g. Microsoft Learn pages where only a banner was kept).
- Snippets get "…" only when cut; `SearchService` validation and query prefix have tests.
- UI ignores out-of-date list responses and keeps Retry errors separate from list errors.
- `build.ps1` starts Postgres for the tests; `run.ps1` fails loudly when Postgres/Ollama aren't ready or the model pull fails, and stops both processes when either exits. Shared container helpers live in `containers.ps1`.
- `Content` stays: re-chunking or re-indexing without refetching will read it.
- Accepted, not fixed: extracted text glues neighbouring words together (`docs/issues_accepted.md`).
- [x] done

## Retro
Where each review finding is prevented next time:
- Unchecked native exit codes in scripts: `guard_edits.py` denies `.ps1` edits that don't check `$LASTEXITCODE`.
- New types missing from the plan: `done_check.py` sends the agent back until each new non-test type is named in a plan.
- HNSW returning fewer rows than asked: a Data access line in the coding guideline (`ef_search`).
- Timeout around only part of an external call: `add-integration` skill step and test.
- Untested validation and stale UI responses: `add-endpoint` skill steps.
- Children changed without an `Include`: `rules-review` skill check.
- [x] done

### Stage 4: Index 4 bookmarks at a time
A speed test (16 public pages, 2026-10-31) showed that loading pages takes most of the indexing time and mostly waits on the network: 4 at a time took 14 s against 31–35 s one at a time. Embedding gained nothing (Ollama handles one request at a time), and Lightpanda handled 4 connections without errors.
- **Affected existing features:** the index worker. Up to 4 bookmarks are indexed at once instead of one, still oldest first. While a Pending bookmark is being indexed, a re-import that updates its title waits until that bookmark is done (up to about 40 s).
- **Behaviour:** `IndexWorker` runs 4 loops. Each `IndexNext` runs in a transaction and claims the oldest Pending bookmark nobody else holds (`FOR UPDATE SKIP LOCKED`), so two loops never index the same bookmark. If the API stops mid-index, the transaction rolls back and the bookmark stays Pending; no new status is needed.
- **Test seam:** `IndexService` against the real test DB with two `DbContext`s: while one `IndexNext` hangs on its fetch, a second one indexes the next bookmark; when the first is cancelled, its bookmark is still Pending.
- **New types:** none.
- **Files touched:** `IndexService.cs`, `BookmarkRepository.cs` (`GetNextPendingOrNull` becomes `LockNextPendingOrNull`), `IndexWorker.cs`, `IndexServiceTests.cs`, `TestDatabase.cs` (a second connection without wiping the tables), docs.
- **Result:** on the real bookmarks, about 30 bookmarks a minute became Indexed, against about 7 one at a time.
- [x] done

## Out of scope
- Authentication (single local user).
- Other export formats, a Playwright fetcher and other embedding APIs. These are only seams for now.
- Scheduled re-indexing, deleting bookmarks, and import history.

## Verification
- `dotnet build -warnaserror` and `dotnet test` stay green each stage (enforced by the Stop hook).
- **End to end:**
  1. Run `build.ps1` (starts Postgres for the tests), then `run.ps1`.
  2. Import a real Firefox export at http://localhost:3000.
  3. Watch the Pending count fall while Indexed rises, and check that the logs show in Seq (http://localhost:8081).
  4. Force a failure, for example a dead URL, and use Retry.
  5. Search for a phrase from a known page and a semantically related query, and confirm both find it.
- Check that `Data\postgres`, `Data\ollama` and `Logs\seq` fill up and survive a container restart.
