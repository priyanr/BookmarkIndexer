// Every call to the API goes through this file; thunks in the store call these functions.

async function send(path, options) {
  const response = await fetch(path, options);
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(body?.detail || body?.title || `Request failed (${response.status})`);
  }
  return body;
}

export function importBookmarks(file) {
  const form = new FormData();
  form.append('file', file);
  return send('/api/imports', { method: 'POST', body: form });
}

export function getBookmarks(status, page) {
  const query = new URLSearchParams({ page });
  if (status) {
    query.set('status', status);
  }
  return send(`/api/bookmarks?${query}`);
}

export function getBookmarkCounts() {
  return send('/api/bookmarks/counts');
}

export function retryBookmark(id) {
  return send(`/api/bookmarks/${id}/retry`, { method: 'POST' });
}

export function retryFailedBookmarks() {
  return send('/api/bookmarks/retry-failed', { method: 'POST' });
}
