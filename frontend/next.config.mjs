/** @type {import('next').NextConfig} */
const nextConfig = {
  // Static export: Tauri serves the files from disk, so there is no Node server at runtime.
  output: "export",
  images: { unoptimized: true },
  // Every route becomes folder/index.html, which Tauri serves without a rewrite.
  trailingSlash: true,
  reactStrictMode: true,
};

export default nextConfig;
