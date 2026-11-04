'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useDispatch, useSelector } from 'react-redux';
import { importFile, importUrls } from '@/store/bookmarksSlice';

export default function ImportPage() {
  const dispatch = useDispatch();
  const { importing, importResult, importError } = useSelector((state) => state.bookmarks);
  const [file, setFile] = useState(null);
  const [urls, setUrls] = useState('');

  function submitFile(event) {
    event.preventDefault();
    dispatch(importFile(file));
  }

  function submitUrls(event) {
    event.preventDefault();
    dispatch(importUrls(urls));
  }

  return (
    <section>
      <h1>Import bookmarks</h1>
      <p>Export your bookmarks to HTML, then pick that file here.</p>
      <ul>
        <li>Firefox: Bookmarks › Manage bookmarks › Import and Backup › Export Bookmarks to HTML</li>
        <li>Chrome: Bookmarks and lists › Bookmark manager › ⋮ › Export bookmarks</li>
        <li>Edge: Favorites › ⋯ › Export favorites</li>
      </ul>
      <form onSubmit={submitFile} className="row">
        <input type="file" accept=".html,.htm" onChange={(e) => setFile(e.target.files[0] ?? null)} />
        <button type="submit" disabled={!file || importing}>
          {importing ? 'Importing…' : 'Import'}
        </button>
      </form>
      <h2>Or paste URLs</h2>
      <p>Paste one URL per line. New ones are added as Pending; URLs already imported are left as they are.</p>
      <form onSubmit={submitUrls} className="column">
        <textarea rows={8} value={urls} onChange={(e) => setUrls(e.target.value)} placeholder="https://example.com/page" />
        <button type="submit" disabled={!urls.trim() || importing}>
          {importing ? 'Importing…' : 'Import URLs'}
        </button>
      </form>
      {importError && <p className="error">{importError}</p>}
      {importResult && (
        <p className="notice">
          Added {importResult.added}, updated {importResult.updated}, unchanged {importResult.skipped}.{' '}
          <Link href="/bookmarks?status=Pending">See pending bookmarks</Link>
        </p>
      )}
    </section>
  );
}
