import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone", // smaller production image — see dashboard/Dockerfile
};

export default nextConfig;
