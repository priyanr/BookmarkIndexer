// The API runs on its own port; proxying /api keeps the browser on one origin, so the API needs no CORS.
const apiUrl = process.env.API_URL || 'http://localhost:5080';

module.exports = {
  output: 'standalone',
  async rewrites() {
    return [{ source: '/api/:path*', destination: `${apiUrl}/api/:path*` }];
  },
};
