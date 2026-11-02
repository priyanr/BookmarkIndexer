# LLM-assisted search (options, not yet approved)

## Problem
A search for `london cinima history` put the article we wanted (about the director who opened the city's first
cinemas) at rank 15, below many unrelated London pages. With the spelling fixed (`london cinema history`,
`uk cinema`, `cinema history`) it ranks 1.

Why (`BookmarkRepository.Search`):
- `websearch_to_tsquery` ANDs every word (`london & cinima & history`). One misspelt word means no full-text hits.
- That leaves only the vector half. Its embedding of the misspelt query mostly matches "London", which a large share of
  the bookmarks mention. A top score of 1/61 ≈ 0.0164 shows a vector-only result, since a page that matches both
  halves scores about 0.03.
- The query always returns 20 results with no relevance cutoff, so the bottom of the list is filler.

## Affected existing features
Search: ranking and possibly the number of results change. Each option below needs explicit confirmation before it is
built.

## Prerequisite
Ollama only has `nomic-embed-text` (embedding only). Options 1–3 need a small chat model pulled, e.g. `qwen2.5:3b` or
`llama3.2:3b`, and an `IChatClient` (Microsoft.Extensions.AI, via OllamaSharp, which is already referenced) registered
next to the embedding generator.

## Options

### 1. Rewrite the query before searching (recommended first)
The LLM fixes spelling and adds related words: `london cinima history` → `london cinema film history uk`.
- Pros: fixes this case directly; one short call (~0.3–1 s on a 3B model); the SQL stays the same.
- Cons: it can "correct" proper names it doesn't know (e.g. the director's surname). To avoid that, search both the original and the
  rewritten query and fuse the results.
- Test seam: `SearchService` with a fake `IChatClient` that returns a fixed rewrite, against the real test database.

### 2. Rerank the top candidates
Hybrid search returns ~30 candidates. The LLM scores each title and snippet against the query and drops the
irrelevant ones.
- Pros: removes the irrelevant tail, which gives a real cutoff instead of always 20 results.
- Cons: slowest option, ~3–10 s per search locally; cannot recover a page that was not among the candidates.

### 3. Answer the question from the results
The LLM reads the top pages and replies with an answer plus links.
- Pros: the nicest to use.
- Cons: the most work and the slowest; it doesn't improve the result list itself.

### Non-LLM alternative: typo-tolerant full-text
Trigram matching with `pg_trgm` (ships with Postgres) lets `cinima` match `cinema`. A few milliseconds, and no new
model. Could be combined with a score cutoff for results that only one half matched.

## Workarounds today
- Use 2–3 distinctive words, spelled correctly.
- Skip words most bookmarks contain, such as `london`.
- Use `or` to match any word instead of all of them: `london cinema or history`.
- Search for a remembered name, such as the director's surname.

## Out of scope
Changing the embedding model or re-indexing bookmarks.
