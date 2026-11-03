# View page content

## Goal
From the bookmarks list, open the page content (the text extracted when the bookmark was indexed) of an Indexed
bookmark, to check what was captured and why search does or doesn't find it.

## Affected existing features
- Bookmarks list: a "Content" button on Indexed rows; the list API doesn't change.
- Search: a "Content" button on each result (Stage 2); the search API and ranking don't change.

## Stages
### Stage 1: Content endpoint and viewer
- Behaviour: `GET /api/bookmarks/{id}/content` returns `{ content }` for an Indexed bookmark; 404 for an unknown id,
  400 "Only indexed bookmarks have content." otherwise. The list shows a "Content" button on Indexed rows that opens
  the text in an overlay with its character count. The text is loaded only when opened, not with the list.
- Test seam: `BookmarkService` with a real database (`BookmarkServiceTests`).
- New types: `BookmarkContentDto` (Shared/Types/Bookmarks): the response, so the UI's JSON client can read it.
- Files touched: `BookmarkService.cs`, `BookmarksController.cs`, `BookmarkServiceTests.cs`, `bookmarksApi.js`,
  `bookmarksSlice.js`, `bookmarks/page.js`, `globals.css`, docs.
- [x] done

### Stage 2: Content from search results
- Behaviour: each search result has a "Content" button that opens the same overlay. The overlay is copied from the
  bookmarks page, not shared (second copy; rule of three).
- Test seam: none new; the endpoint is covered by Stage 1. Checked by hand.
- New types: none.
- Files touched: `search/page.js`, docs.
- [x] done

## Out of scope
- Showing the separate chunks.
- Content of Failed or Pending bookmarks (a re-imported bookmark keeps old content, but it isn't shown until it is Indexed again).
- Logging the content: page contents are never logged.
