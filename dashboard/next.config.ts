import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Standalone output is for the self-hosted Docker image (see dashboard/Dockerfile)
  // — Vercel explicitly warns against it, since it changes the build output shape
  // in a way that breaks Vercel's own serverless routing (every route 404s).
  // Vercel sets VERCEL=1 during its build, so skip it there.
  output: process.env.VERCEL ? undefined : "standalone",
};

export default nextConfig;
