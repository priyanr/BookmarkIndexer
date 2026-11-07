import { basePath, getTokens, refreshTokens, startLogin } from './authApi';

// Every API call goes through send: it adds the base path and, once logged in, the access token.
export async function send(path, options = {}) {
  const response = await fetchWithToken(path, options);
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(body?.detail || body?.title || `Request failed (${response.status})`);
  }
  return body;
}

async function fetchWithToken(path, options) {
  let tokens = getTokens();
  if (!tokens) {
    return fetch(basePath + path, options);
  }
  if (tokens.expiresAt - Date.now() < 30_000) {
    tokens = await refreshTokens();
  }
  if (tokens) {
    const response = await fetch(basePath + path, withToken(tokens));
    if (response.status !== 401) {
      return response;
    }
    tokens = await refreshTokens();
    if (tokens) {
      return fetch(basePath + path, withToken(tokens));
    }
  }
  startLogin();
  // The page is leaving for the login provider; nothing should render an error meanwhile.
  return new Promise(() => {});

  function withToken(current) {
    return { ...options, headers: { ...options.headers, Authorization: `Bearer ${current.accessToken}` } };
  }
}
