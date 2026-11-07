// Every call to the API goes through this file; thunks in the store call these functions.

import { send } from './http';

export function importBookmarks(file) {
  const form = new FormData();
  form.append('file', file);
  return send('/api/imports', { method: 'POST', body: form });
}

export function importUrls(urls) {
  return send('/api/imports/urls', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ urls }),
  });
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

export function getBookmarkContent(id) {
  return send(`/api/bookmarks/${id}/content`);
}

export function retryBookmark(id) {
  return send(`/api/bookmarks/${id}/retry`, { method: 'POST' });
}

export function retryFailedBookmarks() {
  return send('/api/bookmarks/retry-failed', { method: 'POST' });
}
