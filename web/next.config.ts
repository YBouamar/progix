import type { NextConfig } from "next";

// L'API est appelée sous /api/* depuis le navigateur : même origine, donc le cookie de session suit.
const apiUrl = process.env.API_URL ?? "http://localhost:8080";

const nextConfig: NextConfig = {
  output: "standalone",
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiUrl}/api/:path*` }];
  },
};

export default nextConfig;
