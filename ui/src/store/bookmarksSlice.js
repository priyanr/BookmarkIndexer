import { createAsyncThunk, createSlice } from '@reduxjs/toolkit';
import * as api from '@/api/bookmarksApi';

export const fetchBookmarks = createAsyncThunk('bookmarks/fetch', ({ status, page }) => api.getBookmarks(status, page));
export const fetchCounts = createAsyncThunk('bookmarks/fetchCounts', () => api.getBookmarkCounts());
export const importFile = createAsyncThunk('bookmarks/import', (file) => api.importBookmarks(file));

// After a retry the list and counts are stale, so both are reloaded for the page being viewed.
export const retryBookmark = createAsyncThunk('bookmarks/retry', async ({ id, status, page }, { dispatch }) => {
  await api.retryBookmark(id);
  dispatch(fetchBookmarks({ status, page }));
  dispatch(fetchCounts());
});
export const retryFailed = createAsyncThunk('bookmarks/retryFailed', async ({ status, page }, { dispatch }) => {
  await api.retryFailedBookmarks();
  dispatch(fetchBookmarks({ status, page }));
  dispatch(fetchCounts());
});

const bookmarksSlice = createSlice({
  name: 'bookmarks',
  initialState: {
    list: { items: [], total: 0, page: 1, pageSize: 50 },
    counts: { all: 0, pending: 0, indexed: 0, failed: 0 },
    loading: false,
    listRequestId: null,
    error: null,
    retryError: null,
    importing: false,
    importResult: null,
    importError: null,
  },
  reducers: {},
  extraReducers: (builder) => {
    builder
      // The page polls and switches tabs, so an older response can arrive after a newer request; only the latest counts.
      .addCase(fetchBookmarks.pending, (state, action) => {
        state.loading = true;
        state.listRequestId = action.meta.requestId;
        state.error = null;
      })
      .addCase(fetchBookmarks.fulfilled, (state, action) => {
        if (action.meta.requestId !== state.listRequestId) return;
        state.loading = false;
        state.list = action.payload;
      })
      .addCase(fetchBookmarks.rejected, (state, action) => {
        if (action.meta.requestId !== state.listRequestId) return;
        state.loading = false;
        state.error = action.error.message;
      })
      .addCase(fetchCounts.fulfilled, (state, action) => {
        state.counts = action.payload;
      })
      .addCase(importFile.pending, (state) => {
        state.importing = true;
        state.importResult = null;
        state.importError = null;
      })
      .addCase(importFile.fulfilled, (state, action) => {
        state.importing = false;
        state.importResult = action.payload;
      })
      .addCase(importFile.rejected, (state, action) => {
        state.importing = false;
        state.importError = action.error.message;
      })
      .addCase(retryBookmark.pending, (state) => {
        state.retryError = null;
      })
      .addCase(retryBookmark.rejected, (state, action) => {
        state.retryError = action.error.message;
      })
      .addCase(retryFailed.pending, (state) => {
        state.retryError = null;
      })
      .addCase(retryFailed.rejected, (state, action) => {
        state.retryError = action.error.message;
      });
  },
});

export default bookmarksSlice.reducer;
