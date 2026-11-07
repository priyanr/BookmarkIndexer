'use client';

import { useEffect, useState } from 'react';
import { usePathname } from 'next/navigation';
import { getTokens, isAuthEnabled, loadAuthStatus, logout, startLogin } from '@/api/authApi';

// Renders the app only once the user is logged in, or straight away when the API has login switched off.
export default function AuthGate({ children }) {
  const pathname = usePathname();
  const [ready, setReady] = useState(false);
  const [error, setError] = useState(null);
  const isCallback = pathname === '/auth/callback';

  useEffect(() => {
    if (isCallback) {
      return;
    }
    loadAuthStatus()
      .then((enabled) => {
        if (enabled && !getTokens()) {
          startLogin();
          return;
        }
        setReady(true);
      })
      .catch((e) => setError(e.message));
  }, [isCallback]);

  if (error) {
    return <p className="error">{error}</p>;
  }
  return isCallback || ready ? children : null;
}

export function LogoutButton() {
  return isAuthEnabled() ? <button className="link logout" onClick={logout}>Log out</button> : null;
}
