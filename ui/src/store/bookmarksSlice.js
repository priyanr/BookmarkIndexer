import { createAsyncThunk, createSlice } from '@reduxjs/toolkit';
import * as api from '@/api/bookmarksApi';

export const fetchBookmarks = createAsyncThunk('bookmarks/fetch', ({ status, page }) => api.getBookmarks(status, page));
export const fetchCounts = createAsyncThunk('bookmarks/fetchCounts', () => api.getBookmarkCounts());
export const importFile = createAsyncThunk('bookmarks/import', (file) => api.importBookmarks(file));

const bookmarksSlice = createSlice({
  name: 'bookmarks',
  initialState: {
    list: { items: [], total: 0, page: 1, pageSize: 50 },
    counts: { all: 0, pending: 0, indexed: 0, failed: 0 },
    loading: false,
    error: null,
    importing: false,
    importResult: null,
    importError: null,
  },
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(fetchBookmarks.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchBookmarks.fulfilled, (state, action) => {
        state.loading = false;
        state.list = action.payload;
      })
      .addCase(fetchBookmarks.rejected, (state, action) => {
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
      });
  },
});

export default bookmarksSlice.reducer;
