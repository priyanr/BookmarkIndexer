'use client';

import { useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { search } from '@/store/searchSlice';
import { closeContent, fetchContent } from '@/store/bookmarksSlice';

export default function SearchPage() {
  const dispatch = useDispatch();
  const { query, results, loading, error } = useSelector((state) => state.search);
  const { content, contentError } = useSelector((state) => state.bookmarks);
  const [text, setText] = useState(query);
  const [contentResult, setContentResult] = useState(null);

  function submit(event) {
    event.preventDefault();
    dispatch(search(text));
  }

  function openContent(result) {
    setContentResult(result);
    dispatch(fetchContent(result.bookmarkId));
  }

  function hideContent() {
    setContentResult(null);
    dispatch(closeContent());
  }

  return (
    <section>
      <h1>Search bookmarks</h1>
      <form onSubmit={submit} className="row">
        <input
          className="search"
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Words from the page, or what it was about"
          maxLength={500}
          autoFocus
        />
        <button type="submit" disabled={!text.trim() || loading}>
          {loading ? 'Searching…' : 'Search'}
        </button>
      </form>
      {error && <p className="error">{error}</p>}
      {results && results.length === 0 && <p>No indexed bookmarks match “{query}”.</p>}
      {results && results.length > 0 && (
        <ol className="results">
          {results.map((r) => (
            <li key={r.bookmarkId}>
              <a href={r.url} target="_blank" rel="noreferrer">{r.title || r.url}</a>
              <button className="link" onClick={() => openContent(r)}>Content</button>
              <div className="url">{r.folderPath ? `${r.folderPath} · ` : ''}{r.url}</div>
              <p className="snippet">{r.snippet}</p>
            </li>
          ))}
        </ol>
      )}
      {contentResult && (
        <div className="overlay" onClick={hideContent}>
          <div className="viewer" onClick={(e) => e.stopPropagation()}>
            <div className="row">
              <strong>{contentResult.title || contentResult.url}</strong>
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
