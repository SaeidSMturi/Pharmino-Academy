import path from "node:path";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// Builds the sign-up island into Web/wwwroot/dist/signup with stable file names
// (signup.js / signup.css); the Razor page cache-busts them with asp-append-version.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { "@": path.resolve(__dirname, "src") } },
  define: { "process.env.NODE_ENV": JSON.stringify("production") },
  build: {
    outDir: path.resolve(__dirname, "../Web/wwwroot/dist/signup"),
    emptyOutDir: true,
    cssCodeSplit: false,
    rollupOptions: {
      input: path.resolve(__dirname, "src/main.tsx"),
      output: {
        format: "iife",
        entryFileNames: "signup.js",
        assetFileNames: (info) => (info.name?.endsWith(".css") ? "signup.css" : "assets/[name]-[hash][extname]"),
      },
    },
  },
});
