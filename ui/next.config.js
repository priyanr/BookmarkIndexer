// The API runs on its own port; proxying /api keeps the browser on one origin, so the API needs no CORS.
const apiUrl = process.env.API_URL || 'http://localhost:5080';

module.exports = {
  output: 'standalone',
  // Set when the UI is served under a sub-path (k8s: /bm). The /api rewrite below then answers on <basePath>/api.
  basePath: process.env.NEXT_PUBLIC_BASE_PATH || '',
  async rewrites() {
    return [{ source: '/api/:path*', destination: `${apiUrl}/api/:path*` }];
  },
};
