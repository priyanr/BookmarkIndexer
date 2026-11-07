import { send } from './http';

export function searchBookmarks(query) {
  return send(`/api/search?${new URLSearchParams({ q: query })}`);
}
