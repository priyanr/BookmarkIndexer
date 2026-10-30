'use client';

import { Suspense, useEffect } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useDispatch, useSelector } from 'react-redux';
import { fetchBookmarks, fetchCounts } from '@/store/bookmarksSlice';

const tabs = [
  { status: '', label: 'All', count: 'all' },
  { status: 'Pending', label: 'Pending', count: 'pending' },
  { status: 'Indexed', label: 'Indexed', count: 'indexed' },
  { status: 'Failed', label: 'Failed', count: 'failed' },
];

export default function BookmarksPage() {
  // useSearchParams needs a Suspense boundary so the page can still be prerendered.
  return (
    <Suspense>
      <Bookmarks />
    </Suspense>
  );
}

function Bookmarks() {
  const dispatch = useDispatch();
  const searchParams = useSearchParams();
  const status = searchParams.get('status') ?? '';
  const page = Number(searchParams.get('page') ?? 1);
  const { list, counts, loading, error } = useSelector((state) => state.bookmarks);
  const pageCount = Math.max(1, Math.ceil(list.total / list.pageSize));

  // The worker indexes in the background, so the page refreshes to show progress.
  useEffect(() => {
    function load() {
      dispatch(fetchBookmarks({ status, page }));
      dispatch(fetchCounts());
    }
    load();
    const timer = setInterval(load, 5000);
    return () => clearInterval(timer);
  }, [dispatch, status, page]);

  function href(nextStatus, nextPage) {
    const query = new URLSearchParams();
    if (nextStatus) query.set('status', nextStatus);
    if (nextPage > 1) query.set('page', nextPage);
    return `/bookmarks?${query}`;
  }

  return (
    <section>
      <h1>Bookmarks</h1>
      <nav className="tabs">
        {tabs.map((tab) => (
          <Link key={tab.label} href={href(tab.status, 1)} className={tab.status === status ? 'active' : ''}>
            {tab.label} <span className="count">{counts[tab.count]}</span>
          </Link>
        ))}
      </nav>
      {error && <p className="error">{error}</p>}
      {loading && list.items.length === 0 && <p>Loading…</p>}
      <table className="list">
        <thead>
          <tr>
            <th>Title</th>
            <th>Folder</th>
            <th>Status</th>
            <th>Added</th>
          </tr>
        </thead>
        <tbody>
          {list.items.map((b) => (
            <tr key={b.id}>
              <td>
                <a href={b.url} target="_blank" rel="noreferrer">{b.title || b.url}</a>
                <div className="url">{b.url}</div>
                {b.error && <div className="error">{b.error}</div>}
              </td>
              <td>{b.folderPath}</td>
              <td><span className={`status ${b.status.toLowerCase()}`}>{b.status}</span></td>
              <td>{b.addedAt ? new Date(b.addedAt).toLocaleDateString() : ''}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {!loading && list.items.length === 0 && <p>No bookmarks here. <Link href="/import">Import some</Link>.</p>}
      <div className="row">
        {page > 1 && <Link href={href(status, page - 1)}>‹ Previous</Link>}
        <span>Page {page} of {pageCount}</span>
        {page < pageCount && <Link href={href(status, page + 1)}>Next ›</Link>}
      </div>
    </section>
  );
}
