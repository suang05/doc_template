import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: 'standalone',
  reactStrictMode: true,
  eslint: { ignoreDuringBuilds: true },
  images: {
    unoptimized: true,
  },
  async rewrites() {
    const backendUrl = process.env.DOC_SERVER_INTERNAL_URL || 'http://doc-server:8080';
    return [
      {
        source: '/api/:path*',
        destination: `${backendUrl}/api/:path*`,
      },
      {
        source: '/swagger/:path*',
        destination: `${backendUrl}/swagger/:path*`,
      },
    ];
  },
};

export default nextConfig;
