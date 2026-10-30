'use client';

import { useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { search } from '@/store/searchSlice';

export default function SearchPage() {
  const dispatch = useDispatch();
  const { query, results, loading, error } = useSelector((state) => state.search);
  const [text, setText] = useState(query);

  function submit(event) {
    event.preventDefault();
    dispatch(search(text));
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
              <div className="url">{r.folderPath ? `${r.folderPath} · ` : ''}{r.url}</div>
              <p className="snippet">{r.snippet}…</p>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
