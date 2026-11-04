import { createAsyncThunk, createSlice } from '@reduxjs/toolkit';
import * as api from '@/api/bookmarksApi';

export const fetchBookmarks = createAsyncThunk('bookmarks/fetch', ({ status, page }) => api.getBookmarks(status, page));
export const fetchCounts = createAsyncThunk('bookmarks/fetchCounts', () => api.getBookmarkCounts());
export const importFile = createAsyncThunk('bookmarks/import', (file) => api.importBookmarks(file));
export const importUrls = createAsyncThunk('bookmarks/importUrls', (urls) => api.importUrls(urls));
export const fetchContent = createAsyncThunk('bookmarks/fetchContent', (id) => api.getBookmarkContent(id));

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
    // The bookmark whose page content is open; null when the viewer is closed.
    contentBookmarkId: null,
    content: null,
    contentError: null,
  },
  reducers: {
    closeContent: (state) => {
      state.contentBookmarkId = null;
      state.content = null;
      state.contentError = null;
    },
  },
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
      .addCase(importUrls.pending, (state) => {
        state.importing = true;
        state.importResult = null;
        state.importError = null;
      })
      .addCase(importUrls.fulfilled, (state, action) => {
        state.importing = false;
        state.importResult = action.payload;
      })
      .addCase(importUrls.rejected, (state, action) => {
        state.importing = false;
        state.importError = action.error.message;
      })
      .addCase(fetchContent.pending, (state, action) => {
        state.contentBookmarkId = action.meta.arg;
        state.content = null;
        state.contentError = null;
      })
      // Ignore a response for a bookmark the viewer no longer shows.
      .addCase(fetchContent.fulfilled, (state, action) => {
        if (action.meta.arg !== state.contentBookmarkId) return;
        state.content = action.payload.content;
      })
      .addCase(fetchContent.rejected, (state, action) => {
        if (action.meta.arg !== state.contentBookmarkId) return;
        state.contentError = action.error.message;
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

export const { closeContent } = bookmarksSlice.actions;
export default bookmarksSlice.reducer;
