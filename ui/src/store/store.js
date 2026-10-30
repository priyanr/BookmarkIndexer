import { configureStore } from '@reduxjs/toolkit';
import bookmarks from './bookmarksSlice';
import search from './searchSlice';

export function makeStore() {
  return configureStore({ reducer: { bookmarks, search } });
}
