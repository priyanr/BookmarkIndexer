# Accepted issues

Known problems we decided to leave as they are. Each says what happens, why it stays, and what would fix it.

## Extracted text glues neighbouring words together

- **What happens:** SmartReader's `TextContent`, and AngleSharp's `TextContent` in the `PageText` body fallback, join adjacent list items, table cells and links without a space. On the Tour of C# page this gives text like "trainingAzure training" and "matchingC# feature 2".
- **Effect:** full-text search can't match the glued words on their own ("training" alone doesn't hit "trainingAzure"). Meaning-based (vector) search mostly still finds the page, so hybrid search usually still ranks it.
- **Why it stays:** the fix would change the extracted text of every bookmark, and search works well enough as it is (accepted 2026-10-31).
- **Fix if needed:** insert a space after block and inline elements (`li`, `td`, `a`, `p`, `div`, headings) before reading the text in `PageText`, then set the Indexed bookmarks back to Pending so the worker re-extracts them (Retry only accepts Failed ones).
