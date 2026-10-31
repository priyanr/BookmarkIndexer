# BookmarkIndexer

Indexes a single user's browser bookmarks. It imports a Firefox bookmarks HTML export, loads each URL in headless Lightpanda, extracts the readable text, embeds it with Ollama and stores it in Postgres (pgvector). A Next.js UI imports bookmarks, lists them by index status and searches them with hybrid vector and full-text search. Plan: `docs/plans/bookmark-indexer.md`. Current state: `docs/status.md`; changes and affected features: `docs/history.md`; known issues left as they are: `docs/issues_accepted.md`.

## Commands

- Build: `dotnet build -warnaserror`
- Test: `dotnet test`
- Run (dev): `dotnet run --project BookmarkIndexer.Web` and `npm run dev` in `ui/`. The containers must be running; `run.ps1` starts them.
- New migration: `dotnet ef migrations add <Name> -p BookmarkIndexer.Core -s BookmarkIndexer.Web -o Model/Migrations`
- Publish: `<publish folder>\build.ps1` starts Postgres (the tests need it), then builds, tests and publishes to `Api\` and `Ui\` there. Both scripts share the container helpers in `containers.ps1`.
- Run (published): `<publish folder>\run.ps1` starts the wslc containers (Postgres, Seq, Ollama, Lightpanda), the API on :5080 and the UI on :3000. Seq is at :8081.

## Layout

Follows the Solution layout in `.claude/rules/coding-guideline.md`, with one difference:
- `BookmarkIndexer.Web` is API-only: controllers, workers and `Program.cs`, with no Razor views.
- The UI is a separate Next.js app in `ui/`, written in plain JavaScript with Redux Toolkit. It does not use RTK Query. HTTP calls live in `ui/src/api/*.js` and are called from thunks. Next rewrites proxy `/api/*` to the API.

## Domain

Domain terms are defined in `GLOSSARY.md`. Use those names in code, DTOs, tests and messages. Don't introduce a synonym for a term that is already there; if a term is missing or a request uses a term differently, ask.

## Boundaries

- Browser (fetching pages): `IPageFetcher` (Shared), implemented in `Integrations/Lightpanda/` over CDP on :9222.
- Embeddings: `IEmbeddingGenerator<string, Embedding<float>>` (Microsoft.Extensions.AI), implemented by OllamaSharp against :11434.
- Bookmark export formats: `IBookmarkImporter` (Shared), with the Firefox HTML importer in Core.

Never log: page contents.

## Patterns to copy

Before writing something new, copy the shape of the closest example:

- Endpoint: `Web/Controllers/ImportsController.cs` + `Core/Modules/Imports/ImportService.cs`
- Business error to HTTP 400: throw `BadRequestException` (Shared); `Program.cs` maps it.
- Service test with a real database: `Tests/Imports/ImportServiceTests.cs` using `TestDatabase.CreateEmpty()`
- UI call: a function in `ui/src/api/*.js` → a `createAsyncThunk` in `ui/src/store/*Slice.js` → `useDispatch` in the page

## Exceptions to the rules

- Service and repository tests use the local Postgres container (database `bookmarkindexer_test`) instead of SQLite, because pgvector and tsvector need real Postgres.
