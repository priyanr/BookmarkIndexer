import { createAsyncThunk, createSlice } from '@reduxjs/toolkit';
import * as api from '@/api/searchApi';

export const search = createAsyncThunk('search/run', (query) => api.searchBookmarks(query));

const searchSlice = createSlice({
  name: 'search',
  initialState: { query: '', results: null, loading: false, error: null },
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(search.pending, (state, action) => {
        state.query = action.meta.arg;
        state.loading = true;
        state.error = null;
      })
      .addCase(search.fulfilled, (state, action) => {
        state.loading = false;
        state.results = action.payload;
      })
      .addCase(search.rejected, (state, action) => {
        state.loading = false;
        state.error = action.error.message;
      });
  },
});

export default searchSlice.reducer;
