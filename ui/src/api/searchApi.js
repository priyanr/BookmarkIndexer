export async function searchBookmarks(query) {
  const response = await fetch(`/api/search?${new URLSearchParams({ q: query })}`);
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(body?.detail || body?.title || `Request failed (${response.status})`);
  }
  return body;
}
