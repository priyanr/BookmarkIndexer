import { configureStore } from '@reduxjs/toolkit';
import bookmarks from './bookmarksSlice';

export function makeStore() {
  return configureStore({ reducer: { bookmarks } });
}
