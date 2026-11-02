# BookmarkIndexer

Search your browser bookmarks by what the pages say, not just by their titles.

BookmarkIndexer imports a Firefox bookmarks export, loads every page in a headless browser, extracts the readable
text and indexes it for hybrid search: meaning-based (vector) search and keyword (full-text) search, merged into one
ranking. Everything runs on your machine; no page text or query leaves it.

It is built for one person and has no login. Run it on localhost only.

## What it does

- **Import** a Firefox bookmarks HTML export. New URLs become Pending; URLs you already have keep their indexed text
  and get their title and folder updated.
- **Index** in the background: each Pending bookmark is loaded in [Lightpanda](https://lightpanda.io) (a headless
  browser), its readable text is extracted with a Readability port, split into chunks and embedded with
  [Ollama](https://ollama.com) (`nomic-embed-text`). Four bookmarks are indexed at a time, each with a 30 s limit.
  - Pages behind an AWS WAF JavaScript challenge (IMDb, HowStuffWorks) are read after the challenge reloads them.
  - Stack Exchange questions (Stack Overflow, Server Fault, Super User, Ask Ubuntu, `*.stackexchange.com`) are read
    from the Stack Exchange API, because Cloudflare blocks headless browsers there. Without an API key the API allows
    300 requests a day; the rest fail with the API's message and can be retried the next day.
- **Bookmarks list** with All / Pending / Indexed / Failed tabs, the error for each failed bookmark, and Retry.
- **Search**: the 50 nearest chunks by embedding and the 50 best full-text matches are merged with reciprocal rank
  fusion, and the top 20 bookmarks are shown with a snippet.

## How it fits together

```
 Browser ──► UI (Next.js, :3000) ──/api──► API (ASP.NET Core, :5080) ──► Postgres + pgvector
                                              │      background worker
                                              ├──► Lightpanda (CDP, :9222)   loads pages
                                              ├──► Ollama (:11434)           embeddings
                                              └──► api.stackexchange.com     Stack Exchange questions
```

| Folder | What it holds |
|---|---|
| `BookmarkIndexer.Shared` | DTOs, enums, exceptions and the boundary interfaces (`IPageFetcher`, `IBookmarkImporter`) |
| `BookmarkIndexer.Core` | Entities, `AppDbContext`, migrations, services (import, indexing, search) |
| `BookmarkIndexer.Integrations` | Lightpanda and Stack Exchange page fetchers |
| `BookmarkIndexer.Web` | Controllers, the indexing worker and `Program.cs` (API only) |
| `BookmarkIndexer.Tests` | xUnit tests; service and search tests run against real Postgres |
| `ui/` | Next.js app (JavaScript, Redux Toolkit) |

The API applies database migrations itself on start.

## Run it in containers

You need Docker with Compose v2. The first start downloads the images and the embedding model (about 300 MB).

### Windows

1. Install [Docker Desktop](https://docs.docker.com/desktop/setup/install/windows-install/) with the WSL 2 backend
   and start it.
2. In PowerShell:

   ```powershell
   git clone https://github.com/priyanr/BookmarkIndexer.git
   cd BookmarkIndexer
   docker compose up -d --build
   ```

3. Open http://localhost:3000, go to **Import** and upload your bookmarks file.

### Linux

1. Install [Docker Engine](https://docs.docker.com/engine/install/) and the Compose plugin (`docker compose version`
   should print v2.x). Add yourself to the `docker` group or prefix the commands with `sudo`.
2. In a terminal:

   ```bash
   git clone https://github.com/priyanr/BookmarkIndexer.git
   cd BookmarkIndexer
   docker compose up -d --build
   ```

3. Open http://localhost:3000, go to **Import** and upload your bookmarks file.

### Export your Firefox bookmarks

Firefox → Bookmarks → **Manage Bookmarks** (Ctrl+Shift+O) → **Import and Backup** → **Export Bookmarks to HTML…**

### Day to day

The commands are the same on Windows and Linux.

| Task | Command |
|---|---|
| Stop | `docker compose stop` |
| Start again | `docker compose up -d` |
| Update after `git pull` | `docker compose up -d --build` |
| API log | `docker compose logs -f api` (also kept for 14 days in the `logs` volume) |
| Remove everything, including your data | `docker compose down -v` |

Your bookmarks and their index live in the `postgres` volume and the model in the `ollama` volume, so they survive
`stop`, `down` and rebuilds. Only `down -v` deletes them.

Compose publishes Postgres (5432), Ollama (11434) and Lightpanda (9222) on localhost as well, for development. If
one of those ports is already in use on your machine, stop that service or remove the `ports:` line in `compose.yaml`.

Ollama runs on the CPU here. To use an NVIDIA GPU, see
[GPU support in Compose](https://docs.docker.com/compose/how-tos/gpu-support/) and add it to the `ollama` service.

## Develop

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org) 20.9 or later.

```bash
# The services the API needs, in containers
docker compose up -d postgres ollama ollama-pull lightpanda

# API on http://localhost:5080
dotnet run --project BookmarkIndexer.Web

# UI on http://localhost:3000, in a second terminal
cd ui
npm install
npm run dev
```

- Build: `dotnet build -warnaserror`
- Test: `dotnet test` (needs the `postgres` container; tests use their own `bookmarkindexer_test` database)
- New migration: `dotnet ef migrations add <Name> -p BookmarkIndexer.Core -s BookmarkIndexer.Web -o Model/Migrations`

### Configuration

Settings are in `BookmarkIndexer.Web/appsettings.json`. Override any of them with an environment variable, using
`__` for nesting (`compose.yaml` does this for the container addresses).

| Setting | Default |
|---|---|
| `ConnectionStrings__Db` | `Host=localhost;Port=5432;Database=bookmarkindexer;Username=postgres;Password=postgres` |
| `Lightpanda__CdpUrl` | `ws://127.0.0.1:9222` |
| `Ollama__Url` | `http://localhost:11434` |
| `Ollama__EmbeddingModel` | `nomic-embed-text` |
| `Serilog__WriteTo__File__Args__path` | `Logs/api/log-.txt`, relative to the API's working folder |

The UI proxies `/api/*` to `API_URL` (default `http://localhost:5080`). Next.js fixes it at build time, so set it
before `npm run build`.

## Known limitations

- Firefox HTML exports only. Chrome and Edge export the same Netscape format, but only Firefox exports are tested.
- Sites behind Cloudflare and similar bot checks (Medium, Quora, NYTimes and others), login walls and dead pages fail
  to index. Their error is shown and you can Retry them.
- Full-text search needs every word of the query to appear in one chunk. A misspelt word leaves only vector search,
  which is fuzzier. Two or three distinctive, correctly spelt words work best.
- No authentication: anyone who can reach port 3000 can read and change your bookmarks.

## How this was built

The repository also holds the setup used to build it with [Claude Code](https://claude.com/claude-code): the coding,
development and workflow rules in `.claude/rules/`, hooks that block a stage from finishing with warnings or failing
tests, skills for adding endpoints and integrations, and the plans, status and history in `docs/`.

## License

[MIT](LICENSE)
