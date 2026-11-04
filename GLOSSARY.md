# Glossary

The words this system uses, what they mean here, and the name they have in code. When a term means something different from everyday use, say so. Add a term when it first causes a question; don't try to write the whole domain up front.

| Term | Meaning in this system | In code | Not the same as |
|------|------------------------|---------|-----------------|
| Bookmark | One saved URL from a browser export. Unique by URL. | `Bookmark` entity | Page content: the text extracted when the bookmark is indexed |
| Import | Loading a bookmark export file, or a pasted list of URLs, into the system. New URLs become Pending, and existing ones keep their content. | `ImportService`, `IBookmarkImporter` | Indexing: fetching and embedding the page |
| Index status | Where a bookmark is in processing: Pending, Indexed or Failed. | `IndexStatus` enum (Shared) | |
| Chunk | A slice of a bookmark's extracted text, with its embedding and full-text vector. | `BookmarkChunk` entity | |

## Decisions

Short records of decisions that shaped the code and would otherwise get "fixed" by someone who doesn't know the reason. One line each, newest first.

- 2026-10: Words glued together in extracted text are accepted for now (`docs/issues_accepted.md`); fixing it changes every bookmark's text.
- 2026-10: `Bookmark.Content` keeps the full extracted text although search reads only chunks, so chunks can be rebuilt without refetching the page.
- 2026-10: Search raises `hnsw.ef_search` to 100, because the HNSW default of 40 would silently cap the 50 vector candidates.
- 2026-10: Tests use the local Postgres container, not SQLite, because search depends on pgvector and tsvector.
- 2026-10: The swap points for the export format, browser and embeddings are not named `*Provider`, because ArchitectureTests bans that suffix. They are `IBookmarkImporter`, `IPageFetcher` and `IEmbeddingGenerator`.
