'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { completeLogin, startLogin } from '@/api/authApi';

// The login provider sends the browser back here with ?code=...&state=...
export default function AuthCallbackPage() {
  const router = useRouter();
  const [error, setError] = useState(null);

  useEffect(() => {
    const query = new URLSearchParams(window.location.search);
    if (query.get('error')) {
      setError(`Login was refused: ${query.get('error')}`);
      return;
    }
    completeLogin(query.get('code'), query.get('state'))
      .then((returnTo) => router.replace(returnTo))
      .catch((e) => setError(e.message));
  }, [router]);

  if (!error) {
    return <p>Logging in…</p>;
  }
  return (
    <div className="column">
      <p className="error">{error}</p>
      <button onClick={startLogin}>Log in again</button>
    </div>
  );
}
