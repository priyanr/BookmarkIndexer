'use client';

import { Suspense, useEffect, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useDispatch, useSelector } from 'react-redux';
import { closeContent, fetchBookmarks, fetchContent, fetchCounts, retryBookmark, retryFailed } from '@/store/bookmarksSlice';

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
  const { list, counts, loading, error, retryError, content, contentError } = useSelector((state) => state.bookmarks);
  // Kept here rather than looked up in the list, because the 5 s refresh can move the bookmark off this page.
  const [contentBookmark, setContentBookmark] = useState(null);
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

  function openContent(bookmark) {
    setContentBookmark(bookmark);
    dispatch(fetchContent(bookmark.id));
  }

  function hideContent() {
    setContentBookmark(null);
    dispatch(closeContent());
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
      {status === 'Failed' && counts.failed > 0 && (
        <div className="row">
          <button onClick={() => dispatch(retryFailed({ status, page }))}>Retry all {counts.failed} failed</button>
        </div>
      )}
      {error && <p className="error">{error}</p>}
      {retryError && <p className="error">{retryError}</p>}
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
              <td>
                <span className={`status ${b.status.toLowerCase()}`}>{b.status}</span>
                {b.status === 'Failed' && (
                  <button className="link" onClick={() => dispatch(retryBookmark({ id: b.id, status, page }))}>Retry</button>
                )}
                {b.status === 'Indexed' && (
                  <button className="link" onClick={() => openContent(b)}>Content</button>
                )}
              </td>
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
      {contentBookmark && (
        <div className="overlay" onClick={hideContent}>
          <div className="viewer" onClick={(e) => e.stopPropagation()}>
            <div className="row">
              <strong>{contentBookmark.title || contentBookmark.url}</strong>
              {content !== null && <span className="count">{content.length.toLocaleString()} characters</span>}
              <button className="close" onClick={hideContent}>Close</button>
            </div>
            {contentError && <p className="error">{contentError}</p>}
            {content === null && !contentError && <p>Loading…</p>}
            {content !== null && <pre className="content">{content}</pre>}
          </div>
        </div>
      )}
    </section>
  );
}
