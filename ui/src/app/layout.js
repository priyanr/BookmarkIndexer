import Link from 'next/link';
import StoreProvider from './StoreProvider';
import './globals.css';

export const metadata = { title: 'Bookmark Indexer' };

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <body>
        <header className="top">
          <strong>Bookmark Indexer</strong>
          <nav>
            <Link href="/search">Search</Link>
            <Link href="/bookmarks">Bookmarks</Link>
            <Link href="/import">Import</Link>
          </nav>
        </header>
        <main>
          <StoreProvider>{children}</StoreProvider>
        </main>
      </body>
    </html>
  );
}
