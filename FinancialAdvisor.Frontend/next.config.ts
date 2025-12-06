import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        // Note: /api/chat/stream is handled by app/api/chat/stream/route.ts for proper streaming
        // All other /api routes are proxied to the backend
        source: '/api/:path*',
        destination: 'http://localhost:5002/api/:path*',
      },
    ];
  },
  serverExternalPackages: ['jsdom', 'isomorphic-dompurify'],
};

export default nextConfig;
