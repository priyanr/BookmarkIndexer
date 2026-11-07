// Login against the OAuth2 provider (mws.priyan.in) when the API has Auth:Enabled. The browser keeps the tokens:
// the API sends it to the provider, the provider returns it to /auth/callback, and the API swaps the code for tokens.

export const basePath = process.env.NEXT_PUBLIC_BASE_PATH || '';
const tokensKey = 'bmi_tokens';
const stateKey = 'bmi_oauth_state';
const returnToKey = 'bmi_return_to';
let authEnabled = false;
let refreshing = null;

export async function loadAuthStatus() {
  const response = await fetch(`${basePath}/api/auth/status`);
  if (!response.ok) {
    throw new Error(`Could not read the login settings (${response.status})`);
  }
  authEnabled = (await response.json()).enabled;
  return authEnabled;
}

export function isAuthEnabled() {
  return authEnabled;
}

export function getTokens() {
  return JSON.parse(localStorage.getItem(tokensKey));
}

function saveTokens(tokens) {
  const saved = { ...tokens, expiresAt: Date.now() + tokens.expiresIn * 1000 };
  localStorage.setItem(tokensKey, JSON.stringify(saved));
  return saved;
}

export function startLogin() {
  const state = crypto.randomUUID();
  sessionStorage.setItem(stateKey, state);
  sessionStorage.setItem(returnToKey, window.location.pathname.slice(basePath.length) + window.location.search);
  window.location.href = `${basePath}/api/auth/login?${new URLSearchParams({ state })}`;
}

// Returns the page to go back to (without the base path, as the router expects).
export async function completeLogin(code, state) {
  if (!state || state !== sessionStorage.getItem(stateKey)) {
    throw new Error('The login response did not match this browser. Log in again.');
  }
  sessionStorage.removeItem(stateKey);
  const response = await fetch(`${basePath}/api/auth/exchange`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code }),
  });
  if (!response.ok) {
    throw new Error(`Login failed (${response.status})`);
  }
  saveTokens(await response.json());
  return sessionStorage.getItem(returnToKey) || '/';
}

// One refresh at a time: requests that find the token expiring share it. Null when the refresh token was rejected.
export function refreshTokens() {
  refreshing ??= (async () => {
    try {
      const response = await fetch(`${basePath}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: getTokens()?.refreshToken ?? '' }),
      });
      if (response.status === 401 || response.status === 400) {
        localStorage.removeItem(tokensKey);
        return null;
      }
      if (!response.ok) {
        throw new Error(`The login provider is unavailable (${response.status})`);
      }
      return saveTokens(await response.json());
    } finally {
      refreshing = null;
    }
  })();
  return refreshing;
}

export async function logout() {
  const tokens = getTokens();
  localStorage.removeItem(tokensKey);
  if (tokens) {
    // Revoking is best effort: the tokens are already gone from this browser.
    await fetch(`${basePath}/api/auth/logout`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: tokens.refreshToken }),
    }).catch(() => null);
  }
  startLogin();
}
